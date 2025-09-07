using ComputeSharp;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace StableShapeGPU.Properties
{
    // Use the float3 type from ComputeSharp for GPU data
    using float3 = ComputeSharp.Float3;
    using int2 = ComputeSharp.Int2;

    public class GpuMeshSolver
    {
        private readonly GraphicsDevice gpuDevice;

        private ReadWriteBuffer<float3> tempSpringForces;

        // --- GPU Buffers ---
        // Particle data
        private readonly ReadWriteBuffer<float3> particlePositions;
        private readonly ReadWriteBuffer<float3> particleVelocities;
        private readonly ReadWriteBuffer<float3> particleAccelerations;

        // Spring data (read-only on the GPU)
        private readonly ReadOnlyBuffer<int2> springIndices;
        private readonly ReadOnlyBuffer<float> springRestLengths;

        // Mesh topology for reconstruction
        private readonly MeshFace[] meshFaces;

        public GpuMeshSolver(Mesh mesh)
        {
            this.gpuDevice = GraphicsDevice.GetDefault();

            // --- Initialization (was MeshProcess) ---
            var particles = new Particle[mesh.Vertices.Count];
            var springs = new List<Spring>();
            var processedEdges = new List<string>();

            // This part still runs on the CPU once to set up the data
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                particles[i] = new Particle(mesh.Vertices[i]);
            }

            foreach (var face in mesh.Faces)
            {
                if (face.IsQuad)
                {
                    SpringProcess(particles, springs, face.A, face.B, processedEdges);
                    SpringProcess(particles, springs, face.B, face.C, processedEdges);
                    SpringProcess(particles, springs, face.C, face.D, processedEdges);
                    SpringProcess(particles, springs, face.D, face.A, processedEdges);
                }
                else
                {
                    SpringProcess(particles, springs, face.A, face.B, processedEdges);
                    SpringProcess(particles, springs, face.B, face.C, processedEdges);
                    SpringProcess(particles, springs, face.C, face.A, processedEdges);
                }
            }

            // --- Upload to GPU ---
            // Convert CPU data to GPU-friendly formats
            float3[] initialPositions = new float3[particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i].position;
                initialPositions[i] = new float3((float)p.X, (float)p.Y, (float)p.Z);
            }

            int2[] springIdx = new int2[springs.Count];
            float[] restLengths = new float[springs.Count];
            for (int i = 0; i < springs.Count; i++)
            {
                springIdx[i] = new int2(springs[i].vertex0, springs[i].vertex1);
                restLengths[i] = (float)springs[i].restlen;
            }

            // Allocate and copy data to GPU buffers
            this.particlePositions = gpuDevice.AllocateReadWriteBuffer<float3>(initialPositions);
            this.particleVelocities = gpuDevice.AllocateReadWriteBuffer<float3>(particles.Length);
            this.particleAccelerations = gpuDevice.AllocateReadWriteBuffer<float3>(particles.Length);
            this.springIndices = gpuDevice.AllocateReadOnlyBuffer<int2>(springIdx);
            this.springRestLengths = gpuDevice.AllocateReadOnlyBuffer<float>(restLengths);

            this.tempSpringForces = gpuDevice.AllocateReadWriteBuffer<float3>(springs.Count);

            // Keep mesh faces on CPU for final reconstruction
            meshFaces = mesh.Faces.ToArray();
        }

        // Helper for initialization
        private static void SpringProcess(Particle[] particles, List<Spring> springs, int v0, int v1, List<string> processedEdges)
        {
            string edgeID = v0 < v1 ? $"{v0}_{v1}" : $"{v1}_{v0}";
            if (!processedEdges.Contains(edgeID))
            {
                processedEdges.Add(edgeID);
                float restlen = (float)(particles[v1].position - particles[v0].position).Length;
                springs.Add(new Spring(v0, v1, restlen));
            }
        }

        public void SimulateFrame(StableFluid3D fluidSolver)
        {
            // --- Run the entire simulation on the GPU ---

            // 1. Clear accelerations from the previous frame
            gpuDevice.For(particleAccelerations.Length, new ClearFloat3BufferShader(particleAccelerations));

            // 2. Advect particles with the fluid's velocity field
            gpuDevice.For(
                particlePositions.Length,
                new MeshAdvectionShader(
                    particlePositions,
                    particleVelocities,
                    fluidSolver.u, fluidSolver.v, fluidSolver.w, // Pass fluid velocity textures
                    fluidSolver.sizeX, fluidSolver.sizeY, fluidSolver.sizeZ));

            // 3.Move particles based on their velocities
            gpuDevice.For(
                particlePositions.Length,
                new ParticleMoveShader(
                    particlePositions,
                    particleVelocities,
                    particleAccelerations,
                    Particle.timeStep,
                    Particle.damping,
                    Particle.drag));

            // 4. Calculate and apply spring tensions
            gpuDevice.For(
                springIndices.Length,
                new CalculateSpringForcesShader(
                    particlePositions,
                    springIndices,
                    springRestLengths,
                    tempSpringForces, // 写入到临时缓冲区
                    Particle.k,
                    Particle.mass));
            gpuDevice.For(
                particlePositions.Length,
                new GatherForcesShader(
                    particleAccelerations,
                    tempSpringForces, // 从临时缓冲区读取
                    springIndices,
                    springIndices.Length,
                    Particle.mass));

            // 5. Move particles based on physics
            gpuDevice.For(
                particlePositions.Length,
                new ParticleMoveShader(
                    particlePositions,
                    particleVelocities,
                    particleAccelerations,
                    Particle.timeStep,
                    Particle.damping,
                    Particle.drag));
        }

        public Mesh ReconstructMesh()
        {
            Mesh remesh = new Mesh();

            // The ONLY data transfer from GPU to CPU happens here!
            float3[] finalPositions = particlePositions.ToArray();

            // Convert float3 back to Point3d for Rhino
            Point3d[] vertices = new Point3d[finalPositions.Length];
            for (int i = 0; i < finalPositions.Length; i++)
            {
                vertices[i] = new Point3d(finalPositions[i].X, finalPositions[i].Y, finalPositions[i].Z);
            }

            remesh.Vertices.AddVertices(vertices);
            remesh.Faces.AddFaces(meshFaces);

            remesh.Compact();
            remesh.Normals.ComputeNormals();

            return remesh;
        }
    }

    // --- Advection Shader ---
    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.X)]
    public readonly partial struct MeshAdvectionShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float3> positions;
        private readonly ReadWriteBuffer<float3> velocities;
        private readonly ReadWriteTexture3D<float> u, v, w;
        private readonly int sizeX, sizeY, sizeZ;

        public void Execute()
        {
            int i = ThreadIds.X; // The index of the current particle
            float3 pos = positions[i];

            // 1. Clamp position to be within the valid grid bounds for interpolation
            pos.X = Hlsl.Max(0.5f, Hlsl.Min(sizeX - 1.5f, pos.X));
            pos.Y = Hlsl.Max(0.5f, Hlsl.Min(sizeY - 1.5f, pos.Y));
            pos.Z = Hlsl.Max(0.5f, Hlsl.Min(sizeZ - 1.5f, pos.Z));

            //// 2. Determine the integer indices of the 8 surrounding grid cells
            int i0 = (int)pos.X;
            int i1 = i0 + 1;
            int j0 = (int)pos.Y;
            int j1 = j0 + 1;
            int k0 = (int)pos.Z;
            int k1 = k0 + 1;

            // 3. Calculate the fractional interpolation weights
            float s1 = pos.X - i0;
            float t1 = pos.Y - j0;
            float r1 = pos.Z - k0;

            // 4. Sample the velocity components (u, v, w) from the 8 surrounding cells
            //    and perform trilinear interpolation for each component separately.

            // --- Interpolate for U component (X velocity) ---
            float u_x0 = Hlsl.Lerp(u[i0, j0, k0], u[i1, j0, k0], s1);
            float u_x1 = Hlsl.Lerp(u[i0, j1, k0], u[i1, j1, k0], s1);
            float u_x2 = Hlsl.Lerp(u[i0, j0, k1], u[i1, j0, k1], s1);
            float u_x3 = Hlsl.Lerp(u[i0, j1, k1], u[i1, j1, k1], s1);
            float u_y0 = Hlsl.Lerp(u_x0, u_x1, t1);
            float u_y1 = Hlsl.Lerp(u_x2, u_x3, t1);
            float finalU = Hlsl.Lerp(u_y0, u_y1, r1);

            // --- Interpolate for V component (Y velocity) ---
            float v_x0 = Hlsl.Lerp(v[i0, j0, k0], v[i1, j0, k0], s1);
            float v_x1 = Hlsl.Lerp(v[i0, j1, k0], v[i1, j1, k0], s1);
            float v_x2 = Hlsl.Lerp(v[i0, j0, k1], v[i1, j0, k1], s1);
            float v_x3 = Hlsl.Lerp(v[i0, j1, k1], v[i1, j1, k1], s1);
            float v_y0 = Hlsl.Lerp(v_x0, v_x1, t1);
            float v_y1 = Hlsl.Lerp(v_x2, v_x3, t1);
            float finalV = Hlsl.Lerp(v_y0, v_y1, r1);

            // --- Interpolate for W component (Z velocity) ---
            float w_x0 = Hlsl.Lerp(w[i0, j0, k0], w[i1, j0, k0], s1);
            float w_x1 = Hlsl.Lerp(w[i0, j1, k0], w[i1, j1, k0], s1);
            float w_x2 = Hlsl.Lerp(w[i0, j0, k1], w[i1, j0, k1], s1);
            float w_x3 = Hlsl.Lerp(w[i0, j1, k1], w[i1, j1, k1], s1);
            float w_y0 = Hlsl.Lerp(w_x0, w_x1, t1);
            float w_y1 = Hlsl.Lerp(w_x2, w_x3, t1);
            float finalW = Hlsl.Lerp(w_y0, w_y1, r1);

            //// 5. Assemble the final interpolated velocity vector
            float3 fluidVelocity = new float3(finalU,finalV,finalW);

            //// 6. Assign the smooth, interpolated velocity to the particle
            velocities[i] = fluidVelocity;
        }
    }

    // --- Tension Shader ---
    //[AutoConstructor]
    //[EmbeddedBytecode(DispatchAxis.X)]
    //public readonly partial struct ApplyTensionShader : IComputeShader
    //{
    //    private readonly ReadWriteBuffer<float3> positions;
    //    private readonly ReadOnlyBuffer<int2> springIndices;
    //    private readonly ReadOnlyBuffer<float> springRestLengths;
    //    private readonly ReadWriteBuffer<float3> accelerations;
    //    private readonly float k, mass;

    //    public void Execute()
    //    {
    //        int i = ThreadIds.X;
    //        int2 indices = springIndices[i];

    //        float3 pos0 = positions[indices.X];
    //        float3 pos1 = positions[indices.Y];

    //        float3 forceVec = pos1 - pos0;
    //        float currentLength = Hlsl.Length(forceVec);

    //        if (currentLength > 0.001f)
    //        {
    //            float3 force = (forceVec / currentLength) * (k * (currentLength - springRestLengths[i]) / mass);


    //        }
    //    }
    //}

    // --- Particle Move Shader ---

    // --- 第 1 遍: 计算每个弹簧的力 ---
    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.X)]
    public readonly partial struct CalculateSpringForcesShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float3> positions;
        private readonly ReadOnlyBuffer<int2> springIndices;
        private readonly ReadOnlyBuffer<float> springRestLengths;
        private readonly ReadWriteBuffer<float3> springForces; // 输出到这个新缓冲区

        private readonly float k, mass;

        public void Execute()
        {
            int i = ThreadIds.X; // index of the spring
            int2 indices = springIndices[i];

            float3 pos0 = positions[indices.X];
            float3 pos1 = positions[indices.Y];

            float3 forceVec = pos1 - pos0;
            float currentLength = Hlsl.Length(forceVec);

            if (currentLength > 0.001f)
            {
                float3 force = (forceVec / currentLength) * (k * (currentLength - springRestLengths[i]));
                springForces[i] = force;
            }
            else
            {
                springForces[i] = float3.Zero;
            }
        }
    }

    // --- 第 2 遍: 为每个粒子收集力 ---
    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.X)]
    public readonly partial struct GatherForcesShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float3> accelerations;
        private readonly ReadWriteBuffer<float3> springForces;
        private readonly ReadOnlyBuffer<int2> springIndices;

        private readonly int springCount;
        private readonly float mass;

        public void Execute()
        {
            int particleId = ThreadIds.X; // 当前粒子的索引
            float3 totalForce = float3.Zero;

            // 遍历所有弹簧
            for (int i = 0; i < springCount; i++)
            {
                int2 indices = springIndices[i];

                // 如果弹簧的端点0是当前粒子
                if (indices.X == particleId)
                {
                    totalForce += springForces[i];
                }

                // 如果弹簧的端点1是当前粒子
                if (indices.Y == particleId)
                {
                    totalForce -= springForces[i];
                }
            }

            // 一次性写入总加速度
            accelerations[particleId] = totalForce / mass;
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.X)]
    public readonly partial struct ParticleMoveShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float3> positions;
        private readonly ReadWriteBuffer<float3> velocities;
        private readonly ReadWriteBuffer<float3> accelerations;
        private readonly float timeStep, damping, drag;

        public void Execute()
        {
            int i = ThreadIds.X;
            float3 acc = accelerations[i];

            acc -= velocities[i] * drag;
            velocities[i] += acc * timeStep;
            velocities[i] *= damping;
            positions[i] += velocities[i] * timeStep;
        }
    }

    // --- Utility Shader ---
    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.X)]
    public readonly partial struct ClearFloat3BufferShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float3> buffer;
        public void Execute() { buffer[ThreadIds.X] = float3.Zero; }
    }
}
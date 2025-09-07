using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ComputeSharp;
using ComputeSharp.__Internals;
using ComputeSharp.Exceptions;

namespace StableShape.Properties
{
    public class StableFluid3D
    {
        private int sizeX, sizeY, sizeZ;
        private GraphicsDevice gpuDevice;

        //private ReadOnlyTexture3D<float> u_force_buffer;
        //private ReadOnlyTexture3D<float> v_force_buffer;
        //private ReadOnlyTexture3D<float> w_force_buffer;

        // use 3d array to store the field
        private ReadWriteTexture3D<float> u,v,w; // velocity field

        private ReadWriteTexture3D<float> u_prev, v_prev, w_prev; // previous velocity field

        private ReadWriteTexture3D<float> u_add, v_add, w_add;

        private ReadWriteTexture3D<float> density;
        private ReadWriteTexture3D<float> density_prev;

        private ReadWriteTexture3D<float> pressure;
        private ReadWriteTexture3D<float> pressure_prev;

        private ReadWriteTexture3D<float> divergency;

        public Vector3d[,,] vecs;

        // fluid parameters
        private float dt;  // delta time
        private float diff; // diffusion rate
        private float visc; // viscosity

        public StableFluid3D(int width, int height, int depth, float dt = 0.1f, float diff = 0.0001f, float visc = 0.0001f)
        {
            sizeX = width;
            sizeY = height;
            sizeZ = depth;

            gpuDevice = GraphicsDevice.GetDefault();

            // initialize the 3d array
            this.u = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.v = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.w = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);

            this.u_prev = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.v_prev = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.w_prev = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);

            this.u_add = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.v_add = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.w_add = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);

            this.density = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.density_prev = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);

            this.pressure = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.pressure_prev = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);
            this.divergency = gpuDevice.AllocateReadWriteTexture3D<float>(sizeX, sizeY, sizeZ);

            //this.u_force_buffer = gpuDevice.AllocateReadOnlyTexture3D<float>(sizeX, sizeY, sizeZ);
            //this.v_force_buffer = gpuDevice.AllocateReadOnlyTexture3D<float>(sizeX, sizeY, sizeZ);
            //this.w_force_buffer = gpuDevice.AllocateReadOnlyTexture3D<float>(sizeX, sizeY, sizeZ);

            vecs = new Vector3d[sizeX, sizeY, sizeZ];

            this.dt = dt;
            this.diff = diff;
            this.visc = visc;
        }

        //////////////
        public void AddDot(List<Point3d> dots, float amount)
        {
            foreach (var d in dots)
            {
                int i = Clamp((int)d.X, 1, sizeX - 2);
                int j = Clamp((int)d.Y, 1, sizeY - 2);
                int k = Clamp((int)d.Z, 1, sizeZ - 2);
                
                AddDensity(i, j, k, amount);
            }
        }
        public void AddForces(List<Line> forces)
        {
            // If there are no forces, do nothing.
            if (forces.Count == 0) return;

            // 1. Create temporary "delta" arrays on the CPU, initialized to zero.
            var u_force = new float[sizeX, sizeY, sizeZ];
            var v_force = new float[sizeX, sizeY, sizeZ];
            var w_force = new float[sizeX, sizeY, sizeZ];
            float scale = 0.8f;

            // 2. Accumulate all force changes on the CPU. This is very fast.
            foreach (var f in forces)
            {
                int i = Clamp((int)f.From.X, 0, sizeX - 1);
                int j = Clamp((int)f.From.Y, 0, sizeY - 1);
                int k = Clamp((int)f.From.Z, 0, sizeZ - 1);

                u_force[i, j, k] += (float)(f.To.X - f.From.X) * scale;
                v_force[i, j, k] += (float)(f.To.Y - f.From.Y) * scale;
                w_force[i, j, k] += (float)(f.To.Z - f.From.Z) * scale;
            }

            u_add.CopyFrom(u_force);
            v_add.CopyFrom(v_force);
            w_add.CopyFrom(w_force);
        }

        public void AddDensity(int x, int y, int z, float amount)
        {
            // Same pattern for density
            float[,,] density_cpu = density.ToArray();
            density_cpu[x, y, z] += amount;
            density.CopyFrom(density_cpu);
        }

        public void AddVelocity(int x, int y, int z, float amount)
        {
            // Same pattern for density
            gpuDevice.For(sizeX, sizeY, sizeZ, new AddFieldShader(u, u_add));
        }

        //To Set boundary for the fields
        //typeIndex: 1 for X; 2 for Y; 3 for Z; 0 for other
        private void SetBoundary(byte typeIndex, ReadWriteTexture3D<float> f)
        {
            gpuDevice.For(sizeX, sizeY, sizeZ, new SetBoundaryShader(f, typeIndex, sizeX, sizeY, sizeZ));

            // Copy the entire boundary data (or just relevant chunks) to a CPU array
            float[,,] f_cpu = f.ToArray();

            // Explicitly set the 8 corners by average adjacent points
            f_cpu[0, 0, 0] = (f_cpu[1, 0, 0] + f_cpu[0, 1, 0] + f_cpu[0, 0, 1]) * 0.33333f;
            f_cpu[sizeX - 1, 0, 0] = (f_cpu[sizeX - 2, 0, 0] + f_cpu[sizeX - 1, 1, 0] + f_cpu[sizeX - 1, 0, 1]) * 0.33333f;
            f_cpu[sizeX - 1, sizeY - 1, 0] = (f_cpu[sizeX - 2, sizeY - 1, 0] + f_cpu[sizeX - 1, sizeY - 2, 0] + f_cpu[sizeX - 1, sizeY - 1, 1]) * 0.33333f;
            f_cpu[sizeX - 1, sizeY - 1, sizeZ - 1] = (f_cpu[sizeX - 2, sizeY - 1, sizeZ - 1] + f_cpu[sizeX - 1, sizeY - 2, sizeZ - 1] + f_cpu[sizeX - 1, sizeY - 1, sizeZ - 2]) * 0.33333f;
            f_cpu[sizeX - 1, 0, sizeZ - 1] = (f_cpu[sizeX - 2, 0, sizeZ - 1] + f_cpu[sizeX - 1, 1, sizeZ - 1] + f_cpu[sizeX - 1, 0, sizeZ - 2]) * 0.33333f;
            f_cpu[0, sizeY - 1, 0] = (f_cpu[1, sizeY - 1, 0] + f_cpu[0, sizeY - 2, 0] + f_cpu[0, sizeY - 1, 1]) * 0.33333f;
            f_cpu[0, sizeY - 1, sizeZ - 1] = (f_cpu[1, sizeY - 1, sizeZ - 1] + f_cpu[0, sizeY - 2, sizeZ - 1] + f_cpu[0, sizeY - 1, sizeZ - 2]) * 0.33333f;
            f_cpu[0, 0, sizeZ - 1] = (f_cpu[1, 0, sizeZ - 1] + f_cpu[0, 1, sizeZ - 1] + f_cpu[0, 0, sizeZ - 2]) * 0.33333f;

            // Copy the modified CPU array back to the GPU
            f.CopyFrom(f_cpu);
        }

        //private void Diffuse(byte typeIndex, float[,,] f, float[,,] f0, float rate)
        //{
        //    float a = dt * rate * sizeX * sizeY * sizeZ;
        //    for (int n = 0; n < 20; n++)
        //    {
        //        for (int i = 1; i < sizeX - 1; i++)
        //        {
        //            for (int j = 1; j < sizeY - 1; j++)
        //            {
        //                for (int k = 1; k < sizeZ - 1; k++)
        //                {
        //                    f[i, j, k] = (f0[i, j, k] + a * (f[i - 1, j, k] + f[i + 1, j, k] +
        //                                                f[i, j - 1, k] + f[i, j + 1, k] +
        //                                                f[i, j, k - 1] + f[i, j, k + 1])) / (1 + 6 * a);
        //                }
        //            }
        //        }
        //        SetBoundary(typeIndex, f);
        //    }
        //}
        private void Diffuse(byte typeIndex, ReadWriteTexture3D<float> f, ReadWriteTexture3D<float> f0, float rate)
        {
            float a = dt * rate * sizeX * sizeY * sizeZ;
            for (int n = 0; n < 20; n++)
            {
                gpuDevice.For(sizeX, sizeY, sizeZ, new DiffuseShader(f, f0, a));
                SetBoundary(typeIndex, f);
            }
        }


        //Core Method
        //This method is to advect the physics value through the velocity field
        //d is the target field, d0 is the current field
        private void Advect(byte typeIndex, ReadWriteTexture3D<float> d, ReadWriteTexture3D<float> d0, ReadWriteTexture3D<float> u, ReadWriteTexture3D<float> v, ReadWriteTexture3D<float> w)
        {
            //float dt0 = dt * sizeX;

            //for (int i = 1; i < sizeX - 1; i++)
            //{
            //    for (int j = 1; j < sizeY - 1; j++)
            //    {
            //        for (int k = 1; k < sizeZ - 1; k++)
            //        {
            //            float x = i - dt0 * u[i, j, k];
            //            float y = j - dt0 * v[i, j, k];
            //            float z = k - dt0 * w[i, j, k];

            //            //ensure the point within the boundary
            //            x = Math.Max(0.5, Math.Min(sizeX - 1.5, x));
            //            y = Math.Max(0.5, Math.Min(sizeY - 1.5, y));
            //            z = Math.Max(0.5, Math.Min(sizeZ - 1.5, z));

            //            //Find points surrounding
            //            int i0 = (int)x;
            //            int i1 = i0 + 1;
            //            int j0 = (int)y;
            //            int j1 = j0 + 1;
            //            int k0 = (int)z;
            //            int k1 = k0 + 1;

            //            //Calculate the interpolation weight
            //            float s1 = x - i0;
            //            float s0 = 1 - s1;
            //            float t1 = y - j0;
            //            float t0 = 1 - t1;
            //            float r1 = z - k0;
            //            float r0 = 1 - r1;

            //           // Get new value
            //            d[i, j, k] = r0 * (s0 * (t0 * d0[i0, j0, k0] + t1 * d0[i0, j1, k0]) +
            //                            s1 * (t0 * d0[i1, j0, k0] + t1 * d0[i1, j1, k0])) +
            //                        r1 * (s0 * (t0 * d0[i0, j0, k1] + t1 * d0[i0, j1, k1]) +
            //                            s1 * (t0 * d0[i1, j0, k1] + t1 * d0[i1, j1, k1]));
            //        }
            //    }
            //}

            gpuDevice.For(sizeX, sizeY, sizeZ, shader: new AdvectShader(d, d0, u, v, w, dt, sizeX, sizeY, sizeZ));
            SetBoundary(typeIndex, d);
        }

        //Project is to make sure there is no divergency in the velocity field
        private void Project()
        {
            //calculate the div of velocity field
            //for (int i = 1; i < sizeX - 1; i++)
            //{
            //    for (int j = 1; j < sizeY - 1; j++)
            //    {
            //        for (int k = 1; k < sizeZ - 1; k++)
            //        {
            //            div[i, j, k] = -0.5 * ((u[i + 1, j, k] - u[i - 1, j, k]) / sizeX +
            //                            (v[i, j + 1, k] - v[i, j - 1, k]) / sizeY +
            //                            (w[i, j, k + 1] - w[i, j, k - 1]) / sizeZ);
            //            p[i, j, k] = 0;
            //        }
            //    }
            //}

            //Calculate the divergency on GPU
            gpuDevice.For(sizeX, sizeY, sizeZ, new DivergenceShader(u,v,w,divergency,sizeX,sizeY,sizeZ));
            SetBoundary(0, divergency);
            //Clear the pressure field
            gpuDevice.For(sizeX, sizeY, sizeZ, new ClearShader(pressure));
            gpuDevice.For(sizeX, sizeY, sizeZ, new ClearShader(pressure_prev));
            SetBoundary(0, pressure);
            SetBoundary(0, pressure_prev);

            //solve Poisson equation
            for (int n = 0; n < 20; n++)
            {
                //for (int i = 1; i < sizeX - 1; i++)
                //{
                //    for (int j = 1; j < sizeY - 1; j++)
                //    {
                //        for (int k = 1; k < sizeZ - 1; k++)
                //        {
                //            p[i, j, k] = (div[i, j, k] + p[i - 1, j, k] + p[i + 1, j, k] +
                //                                    p[i, j - 1, k] + p[i, j + 1, k] +
                //                                    p[i, j, k - 1] + p[i, j, k + 1]) / 6;
                //        }
                //    }
                //}
                gpuDevice.For(sizeX, sizeY, sizeZ, new PressureSolverShader(pressure_prev, pressure, divergency, sizeX, sizeY, sizeZ));
                (pressure, pressure_prev) = (pressure_prev, pressure);
                SetBoundary(0, pressure);
            }

            //Use the Force delta to adjust the velocity field
            //for (int i = 1; i < sizeX - 1; i++)
            //{
            //    for (int j = 1; j < sizeY - 1; j++)
            //    {
            //        for (int k = 1; k < sizeZ - 1; k++)
            //        {
            //            u[i, j, k] -= 0.5 * sizeX * (p[i + 1, j, k] - p[i - 1, j, k]);
            //            v[i, j, k] -= 0.5 * sizeY * (p[i, j + 1, k] - p[i, j - 1, k]);
            //            w[i, j, k] -= 0.5 * sizeZ * (p[i, j, k + 1] - p[i, j, k - 1]);
            //        }

            //    }
            //}
            gpuDevice.For(sizeX, sizeY, sizeZ, new VelocityCorrectionShader(u, v, w, pressure, sizeX, sizeY, sizeZ));
            SetBoundary(1, u);
            SetBoundary(2, v);
            SetBoundary(3, w);
        }

        public void Update()
        {
            VelocityStep();
            DensityStep();
        }

        private void VelocityStep()
        {
            // Swap velocity
            (u, u_prev) = (u_prev, u);
            (v, v_prev) = (v_prev, v);
            (w, w_prev) = (w_prev, w);

            Diffuse(1, u, u_prev, visc);
            Diffuse(2, v, v_prev, visc);
            Diffuse(3, w, w_prev, visc);

            // Project to make sure div is 0
            Project();

            // Swap velocity
            (u, u_prev) = (u_prev, u);
            (v, v_prev) = (v_prev, v);
            (w, w_prev) = (w_prev, w);

            // Advect - fluid transit itself 
            Advect(1, u, u_prev, u_prev, v_prev, w_prev);
            Advect(2, v, v_prev, u_prev, v_prev, w_prev);
            Advect(3, w, w_prev, u_prev, v_prev, w_prev);

            // Project to make sure div is 0
            Project();
        }

        private void DensityStep()
        {
            // Swap density
            (density, density_prev) = (density_prev, density);

            // diffuse
            Diffuse(0, density, density_prev, diff);

            // Swap back
            (density, density_prev) = (density_prev, density);

            // Advect
            Advect(0, density, density_prev, u, v, w);
        }

        private static void Swap(ref float[,,] a, ref float[,,] b)
        {
            float[,,] temp = a;
            a = b;
            b = temp;
        }

        public float[,,] GetDensity()
        {
            return density.ToArray();
        }

        public Vector3d[,,] GetVelocityField()
        {
            return vecs;
        }

        public List<Line> DrawVector()
        {
            List<Line> lns = new List<Line>();

            // Copy all velocity data from GPU to CPU arrays in one go
            float[,,] u_cpu = u.ToArray();
            float[,,] v_cpu = v.ToArray();
            float[,,] w_cpu = w.ToArray();

            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    for (int k = 0; k < sizeZ; k++)
                    {
                        vecs[i, j, k] = new Vector3d(u_cpu[i, j, k], v_cpu[i, j, k], w_cpu[i, j, k]);
                        Line ln = new Line(new Point3d(i, j, k), vecs[i, j, k]);
                        lns.Add(ln);
                    }
                }
            }
            return lns;
        }

        public static int Clamp(int a, int b, int c)
        {
            if (a < b) return b;
            if (a > c) return c;
            return a;
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct DiffuseShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> f;
        private readonly ReadWriteTexture3D<float> f0;
        private readonly float a;

        public void Execute()
        {
            // 在GPU上并行执行的计算逻辑
            f[ThreadIds.XYZ] = (f0[ThreadIds.XYZ] + a * (f[ThreadIds.X - 1, ThreadIds.Y, ThreadIds.Z] + f[ThreadIds.X + 1, ThreadIds.Y, ThreadIds.Z]
                                                        + f[ThreadIds.X, ThreadIds.Y - 1, ThreadIds.Z] + f[ThreadIds.X, ThreadIds.Y + 1, ThreadIds.Z]
                                                        + f[ThreadIds.X, ThreadIds.Y, ThreadIds.Z - 1] + f[ThreadIds.X, ThreadIds.Y, ThreadIds.Z + 1])) / (1 + 6 * a);
        }

    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct AdvectShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> d;
        private readonly ReadWriteTexture3D<float> d0;
        private readonly ReadWriteTexture3D<float> u, v, w;
        private readonly float dt;
        private readonly int sizeX, sizeY, sizeZ;
        public void Execute()
        {
            int i = ThreadIds.X;
            int j = ThreadIds.Y;
            int k = ThreadIds.Z;

            // 仅处理内部点
            if (i > 0 && i < sizeX - 1 && j > 0 && j < sizeY - 1 && k > 0 && k < sizeZ - 1)
            {
                float dt0 = dt * sizeX;

                // Track the source point
                float x = i - dt0 * u[ThreadIds.XYZ];
                float y = j - dt0 * v[ThreadIds.XYZ];
                float z = k - dt0 * w[ThreadIds.XYZ];

                // boundary check
                x = Hlsl.Max(0.5f, Hlsl.Min(sizeX - 1.5f, x));
                y = Hlsl.Max(0.5f, Hlsl.Min(sizeY - 1.5f, y));
                z = Hlsl.Max(0.5f, Hlsl.Min(sizeZ - 1.5f, z));

                // get surrounding points
                int i0 = (int)x;
                int i1 = i0 + 1;
                int j0 = (int)y;
                int j1 = j0 + 1;
                int k0 = (int)z;
                int k1 = k0 + 1;

                // compute interpolation weights
                float s1 = x - i0;
                float t1 = y - j0;
                float r1 = z - k0;

                // 5. 执行三线性插值
                // 沿着 X 轴插值两次
                float d_x0 = Hlsl.Lerp((float)d0[i0, j0, k0], (float)d0[i1, j0, k0], (float)s1);
                float d_x1 = Hlsl.Lerp((float)d0[i0, j1, k0], (float)d0[i1, j1, k0], (float)s1);
                float d_x2 = Hlsl.Lerp((float)d0[i0, j0, k1], (float)d0[i1, j0, k1], (float)s1);
                float d_x3 = Hlsl.Lerp((float)d0[i0, j1, k1], (float)d0[i1, j1, k1], (float)s1);

                // 沿着 Y 轴插值两次
                float d_y0 = Hlsl.Lerp((float)d_x0, (float)d_x1, (float)t1);
                float d_y1 = Hlsl.Lerp((float)d_x2, (float)d_x3, (float)t1);

                // 最终沿着 Z 轴插值
                d[ThreadIds.XYZ] = Hlsl.Lerp((float)d_y0, (float)d_y1, (float)r1);
            }
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct DivergenceShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> u, v, w;
        private readonly ReadWriteTexture3D<float> div;
        private readonly int sizeX, sizeY, sizeZ;

        public void Execute()
        {
            int i = ThreadIds.X;
            int j = ThreadIds.Y;
            int k = ThreadIds.Z;

            if (i > 0 && i < sizeX - 1 && j > 0 && j < sizeY - 1 && k > 0 && k < sizeZ - 1)
            {
                div[ThreadIds.XYZ] = -0.5f * (
                    (u[i + 1, j, k] - u[i - 1, j, k]) / sizeX + 
                    (v[i, j + 1, k] - v[i, j - 1, k]) / sizeY +
                    (w[i, j, k + 1] - w[i, j, k - 1]) / sizeZ);
            }
            else
            {
                div[ThreadIds.XYZ] = 0;
            }
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct PressureSolverShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> p_in;
        private readonly ReadWriteTexture3D<float> p_out;
        private readonly ReadWriteTexture3D<float> div;
        private readonly int sizeX, sizeY, sizeZ;

        public void Execute()
        {
            int i = ThreadIds.X;
            int j = ThreadIds.Y;
            int k = ThreadIds.Z;

            // 仅计算内部点
            if (i > 0 && i < sizeX - 1 && j > 0 && j < sizeY - 1 && k > 0 && k < sizeZ - 1)
            {
                p_out[ThreadIds.XYZ] = (div[ThreadIds.XYZ] +
                                      p_in[i - 1, j, k] + p_in[i + 1, j, k] +
                                      p_in[i, j - 1, k] + p_in[i, j + 1, k] +
                                      p_in[i, j, k - 1] + p_in[i, j, k + 1]) / 6.0f;
            }
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct VelocityCorrectionShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> u, v, w;
        private readonly ReadWriteTexture3D<float> p;
        private readonly int sizeX, sizeY, sizeZ;
        public void Execute()
        {
            int i = ThreadIds.X;
            int j = ThreadIds.Y;
            int k = ThreadIds.Z;

            // Only compute internal points
            if (i > 0 && i < sizeX - 1 && j > 0 && j < sizeY - 1 && k > 0 && k < sizeZ - 1)
            {
                u[ThreadIds.XYZ] -= 0.5f * (p[i + 1, j, k] - p[i - 1, j, k]) * sizeX;
                v[ThreadIds.XYZ] -= 0.5f * (p[i, j + 1, k] - p[i, j - 1, k]) * sizeY;
                w[ThreadIds.XYZ] -= 0.5f * (p[i, j, k + 1] - p[i, j, k - 1]) * sizeZ;
            }
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct ClearShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> target;

        public void Execute()
        {
            target[ThreadIds.XYZ] = 0;
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct SetBoundaryShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> f;
        private readonly int typeIndex; // 1 for u, 2 for v, 3 for w, 0 for others
        private readonly int sizeX, sizeY, sizeZ;

        public void Execute()
        {
            int i = ThreadIds.X;
            int j = ThreadIds.Y;
            int k = ThreadIds.Z;

            // Check if the current thread is on the boundary
            bool onBoundary = (i == 0 || i == sizeX - 1 || j == 0 || j == sizeY - 1 || k == 0 || k == sizeZ - 1);

            if (onBoundary)
            {
                // Z 
                if (k == 0) f[i, j, 0] = (typeIndex == 3) ? -f[i, j, 1] : f[i, j, 1];
                if (k == sizeZ - 1) f[i, j, sizeZ - 1] = (typeIndex == 3) ? -f[i, j, sizeZ - 2] : f[i, j, sizeZ - 2];

                // Y 
                if (j == 0) f[i, 0, k] = (typeIndex == 2) ? -f[i, 1, k] : f[i, 1, k];
                if (j == sizeY - 1) f[i, sizeY - 1, k] = (typeIndex == 2) ? -f[i, sizeY - 2, k] : f[i, sizeY - 2, k];

                // X
                if (i == 0) f[0, j, k] = (typeIndex == 1) ? -f[1, j, k] : f[1, j, k];
                if (i == sizeX - 1) f[sizeX - 1, j, k] = (typeIndex == 1) ? -f[sizeX - 2, j, k] : f[sizeX - 2, j, k];
            }
        }
    }

    [AutoConstructor]
    [EmbeddedBytecode(DispatchAxis.XYZ)]
    public readonly partial struct AddFieldShader : IComputeShader
    {
        private readonly ReadWriteTexture3D<float> targetField;
        private readonly ReadWriteTexture3D<float> valuesToAdd;
        public void Execute()
        {
            targetField[ThreadIds.XYZ] += valuesToAdd[ThreadIds.XYZ];
        }
    }


    public class StableFluid2D
    {
        private int sizeX;
        private int sizeY;

        private Mesh mesh;

        // use 2d array to store the field
        private double[,] u;      // X 
        private double[,] v;      // Y
        private double[,] u_prev;
        private double[,] v_prev;
        private double[,] density;
        private double[,] density_prev;

        // fluid parameters
        private double dt;  // delta time
        private double diff; // diffusion rate
        private double visc; // viscosity

        public StableFluid2D(int width, int height, double dt = 0.1f, double diff = 0.0001f, double visc = 0.0001f)
        {
            sizeX = width;
            sizeY = height;

            // initialize the 2d array
            u = new double[sizeX, sizeY];
            v = new double[sizeX, sizeY];
            u_prev = new double[sizeX, sizeY];
            v_prev = new double[sizeX, sizeY];
            density = new double[sizeX, sizeY];
            density_prev = new double[sizeX, sizeY];

            this.dt = dt;
            this.diff = diff;
            this.visc = visc;
        }

        //////////////
        public void AddForce(IList<Line> forces)
        {
            foreach (var f in forces)
            {
                double deltau = f.ToX - f.FromX;
                double deltav = f.ToY - f.FromY;

                AddVelocity((int)f.FromX, (int)f.FromY, deltau, deltav);
            }
        }
        public void AddDot(IList<Point3d> dots, double amount)
        {
            foreach (var d in dots)
            {
                AddDensity((int)d.X, (int)d.Y, amount);
            }
        }
        public void AddVelocity(int x, int y, double amountX, double amountY)
        {
            int i = Clamp(x, 1, sizeX - 2);
            int j = Clamp(y, 1, sizeY - 2);

            u[i, j] += amountX;
            v[i, j] += amountY;
        }

        public void AddDensity(int x, int y, double amount)
        {
            int i = Clamp(x, 1, sizeX - 2);
            int j = Clamp(y, 1, sizeY - 2);

            density[i, j] += amount;
        }

        private void SetBoundary(int typeIndex, double[,] f)
        {
            for (int i = 1; i < sizeX - 1; i++)
            {
                //when the typeIndex is 2, process the velocity on y direction
                if (typeIndex == 2)
                {
                    f[i, 0] = -f[i, 1];
                    f[i, sizeY - 1] = -f[i, sizeY - 2];
                }
                //other cases just set the boudary velociy to be the adjacent one (0 or 1)
                else
                {
                    f[i, 0] = f[i, 1];
                    f[i, sizeY - 1] = f[i, sizeY - 2];
                }
            }

            for (int j = 1; j < sizeY - 1; j++)
            {
                //when the typeIndex is 1, process the velocity on x direction
                if (typeIndex == 1)
                {
                    f[0, j] = -f[1, j];
                    f[sizeX - 1, j] = -f[sizeX - 2, j];
                }
                //other cases just set the boudary velociy to be the adjacent one (0 or 2)
                else
                {
                    f[0, j] = f[1, j];
                    f[sizeX - 1, j] = f[sizeX - 2, j];
                }
            }

            // Explicitly set the corner by average adjacent points
            f[0, 0] = 0.5f * (f[1, 0] + f[0, 1]);
            f[0, sizeY - 1] = 0.5f * (f[1, sizeY - 1] + f[0, sizeY - 2]);
            f[sizeX - 1, 0] = 0.5f * (f[sizeX - 2, 0] + f[sizeX - 1, 1]);
            f[sizeX - 1, sizeY - 1] = 0.5f * (f[sizeX - 2, sizeY - 1] + f[sizeX - 1, sizeY - 2]);
        }

        private void Diffuse(int typeIndex, double[,] f, double[,] f0, double rate)
        {
            double a = dt * rate * sizeX * sizeY;
            for (int k = 0; k < 20; k++)
            {
                for (int i = 1; i < sizeX - 1; i++)
                {
                    for (int j = 1; j < sizeY - 1; j++)
                    {
                        f[i, j] = (f0[i, j] + a * (f[i - 1, j] + f[i + 1, j] +
                                                f[i, j - 1] + f[i, j + 1])) / (1 + 4 * a);
                    }
                }
                SetBoundary(typeIndex, f);
            }
        }

        //Core Method
        //This method is to advect the physics value through the velocity field
        //d is the target array, d0 is the current array
        private void Advect(int typeIndex, double[,] d, double[,] d0, double[,] u, double[,] v)
        {
            double dt0 = dt * sizeX;

            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    double x = i - dt0 * u[i, j];
                    double y = j - dt0 * v[i, j];

                    //ensure the point within the boundary
                    x = Math.Max(0.5f, Math.Min(sizeX - 1.5f, x));
                    y = Math.Max(0.5f, Math.Min(sizeY - 1.5f, y));

                    //Find points surrounding
                    int i0 = (int)x;
                    int i1 = i0 + 1;
                    int j0 = (int)y;
                    int j1 = j0 + 1;

                    //Calculate the interpolation weight
                    double s1 = x - i0;
                    double s0 = 1 - s1;
                    double t1 = y - j0;
                    double t0 = 1 - t1;

                    // Get new value
                    d[i, j] = s0 * (t0 * d0[i0, j0] + t1 * d0[i0, j1]) +
                     s1 * (t0 * d0[i1, j0] + t1 * d0[i1, j1]);
                }
            }
            SetBoundary(typeIndex, d);
        }

        //Project is to make sure there is no divergency in the velocity field
        private void Project(double[,] u, double[,] v, double[,] p, double[,] div)
        {
            //calculate the div of velocity field
            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    div[i, j] = -0.5f * ((u[i + 1, j] - u[i - 1, j]) / sizeX +
                                        (v[i, j + 1] - v[i, j - 1]) / sizeY);
                    p[i, j] = 0;
                }
            }

            SetBoundary(0, div);
            SetBoundary(0, p);

            //solve Poisson equation
            for (int k = 0; k < 20; k++)
            {
                for (int i = 1; i < sizeX - 1; i++)
                {
                    for (int j = 1; j < sizeY - 1; j++)
                    {
                        p[i, j] = (div[i, j] + p[i - 1, j] + p[i + 1, j] + p[i, j - 1] + p[i, j + 1]) / 4;
                    }
                }
            }
            SetBoundary(0, p);

            //Use the Force delta to adjust the velocity field
            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    u[i, j] -= 0.5f * sizeX * (p[i + 1, j] - p[i - 1, j]);
                    v[i, j] -= 0.5f * sizeY * (p[i, j + 1] - p[i, j - 1]);
                }
            }
            SetBoundary(1, u);
            SetBoundary(2, v);
        }

        public void Update()
        {
            VelocityStep();
            DensityStep();
        }

        private void VelocityStep()
        {
            // Swap velocity
            Swap(ref u, ref u_prev);
            Swap(ref v, ref v_prev);

            Diffuse(1, u, u_prev, visc);
            Diffuse(2, v, v_prev, visc);

            // Project to make sure div is 0  
            Project(u, v, u_prev, v_prev);

            // Swap velocity
            Swap(ref u, ref u_prev);
            Swap(ref v, ref v_prev);

            // Advect - fluid transit itself 
            Advect(1, u, u_prev, u_prev, v_prev);
            Advect(2, v, v_prev, u_prev, v_prev);

            // Project to make sure div is 0   
            Project(u, v, u_prev, v_prev);
        }

        private void DensityStep()
        {
            // Swap density
            Swap(ref density, ref density_prev);

            // diffuse  
            Diffuse(0, density, density_prev, diff);

            // Swap back
            Swap(ref density, ref density_prev);

            // Advect  
            Advect(0, density, density_prev, u, v);
        }

        private void Swap(ref double[,] a, ref double[,] b)
        {
            double[,] temp = a;
            a = b;
            b = temp;
        }

        public double[,] GetDensity()
        {
            return density;
        }

        public Mesh GetMesh()
        {
            return mesh;
        }

        public List<Line> DrawVector()
        {
            List<Line> lines = new List<Line>();
            Vector3d[,] vecs = new Vector3d[sizeX, sizeY];
            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    vecs[i, j] = new Vector3d(u[i, j], v[i, j], 0);
                    Line ln = new Line(new Point3d(i, j, 0), vecs[i, j]);
                    lines.Add(ln);
                }
            }
            return lines;
        }

        public int Clamp(int a, int b, int c)
        {
            if (a < b) return b;
            if (a > c) return c;
            return a;
        }
    }
}

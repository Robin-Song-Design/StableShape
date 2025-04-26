using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StableShape.Properties
{
    public static class MeshEditor
    {
        ///Mesh Process
        public static Mesh originalMesh;
        public static List<Spring> springs = new List<Spring>();
        public static Particle[] particles;

        public static void MeshProcess(Mesh m)
        {
            originalMesh = m.DuplicateMesh();
            particles = new Particle[m.Vertices.Count];
            List<string> processedEdges = new List<string>();

            for (int i = 0; i < m.Vertices.Count; i++)
            {
                particles[i] = new Particle(m.Vertices[i]);
                particles[i].index = i;
            }

            foreach (var face in m.Faces)
            {
                if (face.IsQuad)
                {
                    SpringProcess(face.A, face.B, processedEdges);
                    SpringProcess(face.B, face.C, processedEdges);
                    SpringProcess(face.C, face.D, processedEdges);
                    SpringProcess(face.D, face.A, processedEdges);
                }
                else
                {
                    SpringProcess(face.A, face.B, processedEdges);
                    SpringProcess(face.B, face.C, processedEdges);
                    SpringProcess(face.C, face.A, processedEdges);
                }
            }
        }
        public static Mesh ReconstructMesh()
        {
            Mesh remesh = new Mesh();
            foreach (var ptc in particles)
            {
                remesh.Vertices.Add(new Point3d(ptc.position));
            }
            remesh.Faces.AddFaces(originalMesh.Faces);

            remesh.Compact();
            remesh.Normals.ComputeNormals();

            return remesh;
        }

        public static void ApplyAttraction(Point3d pt)
        {
            foreach (var ptc in particles)
            {
                Vector3d force;
                force = new Vector3d(pt) - ptc.position;
                force.Unitize();
                force *= 0.4 / (ptc.position - new Vector3d(pt)).Length;
                ptc.ApplyForce(force);
            }
        }

        public static void ApplyTension()
        {
            //initialize
            foreach (var ptc in MeshEditor.particles)
            {
                ptc.acceleration = new Vector3d(0, 0, 0);
                ptc.forcecounter = 0;
            }

            //To calculate and record the tension
            Vector3d[] tension = new Vector3d[MeshEditor.particles.Length];
            for (int i = 0; i < springs.Count; i++)
            {
                Vector3d force;
                double distance;

                force = Vector3d.Subtract(MeshEditor.particles[springs[i].vertex1].position, MeshEditor.particles[springs[i].vertex0].position);
                distance = force.Length;
                force.Unitize();
                force *= (Particle.k * (distance - springs[i].restlen)) / Particle.mass;
                tension[springs[i].vertex0] += force;
            }

            // To apply the tension
            for (int i = 0; i < MeshEditor.particles.Length; i++)
            {
                if (!MeshEditor.particles[i].clamp)
                {
                    MeshEditor.particles[i].ApplyForce(tension[i]);
                }
            }
        }

        public static void SpringProcess(int v0, int v1, List<string> processedEdges)
        {
            string edgeID = v0 < v1 ? $"{v0}_{v1}" : $"{v1}_{v0}";

            if (!processedEdges.Contains(edgeID))
            {
                processedEdges.Add(edgeID);
                double restlen = Vector3d.Subtract(particles[v1].position, particles[v0].position).Length;

                springs.Add(new Spring(v0, v1, restlen));
            }
        }
    }
}

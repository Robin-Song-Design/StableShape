using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StableShapeGPU.Properties
{
    public class Spring
    {
        public int vertex0; //index of vertex0 of the side
        public int vertex1; //index of vertex1 of the side
        public float restlen;

        public Spring(int v0, int v1, float rest)
        {
            vertex0 = v0;
            vertex1 = v1;
            restlen = rest;
        }
    }


    public class Particle
    {
        public Vector3d position;
        public Vector3d velocity;
        public Vector3d acceleration;
        public int forcecounter;
        public bool clamp;
        public int index;

        internal static float timeStep = 0.1f;//0.05;
        internal static float drag = 0.5f;

        //Particle System properties
        internal static float k = 5f;
        internal static float damping = 0.95f;
        internal static float mass = 1.0f;

        public Particle(Point3d pt)
        {
            position = new Vector3d(pt.X, pt.Y, pt.Z);
            velocity = new Vector3d(0, 0, 0);
            acceleration = new Vector3d(0, 0, 0);
            forcecounter = 0;
        }

        //public void Move()
        //{
        //    if (!clamp)
        //    {
        //        acceleration -= velocity * drag;
        //        velocity += acceleration * timeStep;
        //        velocity *= damping;
        //        position += velocity * timeStep;
        //    }
        //    acceleration *= 0;
        //    forcecounter = 0;
        //}

        //public void ApplyForce(Vector3d force)
        //{
        //    acceleration += force;
        //    forcecounter++;
        //}
    }
}

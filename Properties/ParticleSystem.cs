using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StableShape.Properties
{
    public class Spring
    {
        public int vertex0; //index of vertex0 of the side
        public int vertex1; //index of vertex1 of the side
        public double restlen;

        public Spring(int v0, int v1, double rest)
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

        private static double timeStep = 0.1;//0.05;
        private static double drag = 0.5;

        //Particle System properties
        public static double k = 0.5;
        public static double damping = 0.95;
        public static double mass = 1.0;

        public Particle(Point3d pt)
        {
            position = new Vector3d(pt.X, pt.Y, pt.Z);
            velocity = new Vector3d(0, 0, 0);
            acceleration = new Vector3d(0, 0, 0);
            forcecounter = 0;
        }

        public void Move()
        {
            if (!clamp)
            {
                acceleration -= velocity * drag;
                velocity += acceleration * timeStep;
                velocity *= damping;
                position += velocity * timeStep;
            }
            acceleration *= 0;
            forcecounter = 0;
        }

        public void ApplyForce(Vector3d force)
        {
            acceleration += force;
            forcecounter++;
        }
    }
}

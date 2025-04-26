using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StableShape.Properties
{
    public class VelocityField
    {
        int gridx;
        int gridy;
        int gridz;

        public Vector3d[,,] velocityfield;

        public VelocityField(List<int> size, List<Vector3d> velocities)
        {
            gridx = size[0];
            gridy = size[1];
            gridz = size[2];
            velocityfield = new Vector3d[gridx, gridy, gridz];

            int index = 0;
            for (int i = 0; i < gridx; i++)
            {
                for (int j = 0; j < gridy; j++)
                {
                    for (int k = 0; k < gridz; k++)
                    {
                        velocityfield[i, j, k] = velocities[index];
                        index++;
                    }
                }
            }
        }

        public Vector3d GetVelocityAt(Point3d p)
        {
            int _x = (int)p.X;
            int _y = (int)p.Y;
            int _z = (int)p.Z;

            _x = Clamp(_x, 0, gridx - 1);
            _y = Clamp(_y, 0, gridy - 1);
            _z = Clamp(_z, 0, gridz - 1);

            return velocityfield[_x, _y, _z];
        }

        public static int Clamp(int a, int b, int c)
        {
            if (a < b) return b;
            if (a > c) return c;
            return a;
        }
    }
}

using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace StableShape.Properties
{
    public class StableFluid3D
    {
        private int sizeX;
        private int sizeY;
        private int sizeZ;

        // use 3d array to store the field
        private double[,,] u;//X 
        private double[,,] v;//Y
        private double[,,] w;//Z

        private double[,,] u_prev;
        private double[,,] v_prev;
        private double[,,] w_prev;

        private double[,,] density;
        private double[,,] density_prev;

        private double[,,] pressure;
        private double[,,] divergency;

        public Vector3d[,,] vecs;

        // fluid parameters
        private double dt;  // delta time
        private double diff; // diffusion rate
        private double visc; // viscosity

        public StableFluid3D(int width, int height, int depth, double dt = 0.1, double diff = 0.0001, double visc = 0.0001)
        {
            sizeX = width;
            sizeY = height;
            sizeZ = depth;

            // initialize the 2d array
            u = new double[sizeX, sizeY, sizeZ];
            v = new double[sizeX, sizeY, sizeZ];
            w = new double[sizeX, sizeY, sizeZ];

            u_prev = new double[sizeX, sizeY, sizeZ];
            v_prev = new double[sizeX, sizeY, sizeZ];
            w_prev = new double[sizeX, sizeY, sizeZ];

            density = new double[sizeX, sizeY, sizeZ];
            density_prev = new double[sizeX, sizeY, sizeZ];

            pressure = new double[sizeX, sizeY, sizeZ];
            divergency = new double[sizeX, sizeY, sizeZ];

            vecs = new Vector3d[sizeX, sizeY, sizeZ];

            this.dt = dt;
            this.diff = diff;
            this.visc = visc;
        }

        //////////////
        public void AddForce(List<Line> forces)
        {
            foreach (var f in forces)
            {
                double deltau = f.ToX - f.FromX;
                double deltav = f.ToY - f.FromY;
                double deltaw = f.ToZ - f.FromZ;

                AddVelocity((int)f.FromX, (int)f.FromY, (int)f.FromZ, deltau, deltav, deltaw);
            }
        }
        public void AddDot(List<Point3d> dots, double amount)
        {
            foreach (var d in dots)
            {
                AddDensity((int)d.X, (int)d.Y, (int)d.Z, amount);
            }
        }
        public void AddVelocity(int x, int y, int z, double amountX, double amountY, double amountZ)
        {
            double scale = 0.8;

            int i = Clamp(x, 1, sizeX - 2);
            int j = Clamp(y, 1, sizeY - 2);
            int k = Clamp(z, 1, sizeZ - 2);

            u[i, j, k] += amountX * scale;
            v[i, j, k] += amountY * scale;
            w[i, j, k] += amountZ * scale;
        }

        public void AddDensity(int x, int y, int z, double amount)
        {
            int i = Clamp(x, 1, sizeX - 2);
            int j = Clamp(y, 1, sizeY - 2);
            int k = Clamp(z, 1, sizeZ - 2);

            density[i, j, k] += amount;
        }

        //To Set boundary for the fields
        //typeIndex: 1 for X; 2 for Y; 3 for Z; 0 for other
        private void SetBoundary(byte typeIndex, double[,,] f)
        {
            //Z direction
            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    f[i, j, 0] = typeIndex == 3 ? -f[i, j, 1] : f[i, j, 1];
                    f[i, j, sizeZ - 1] = typeIndex == 3 ? -f[i, j, sizeZ - 2] : f[i, j, sizeZ - 2];
                }
            }

            //Y direction
            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int k = 1; k < sizeZ - 1; k++)
                {
                    f[i, 0, k] = typeIndex == 2 ? -f[i, 1, k] : f[i, 1, k];
                    f[i, sizeY - 1, k] = typeIndex == 2 ? -f[i, sizeY - 2, k] : f[i, sizeY - 2, k];
                }
            }

            //X direction
            for (int j = 1; j < sizeY - 1; j++)
            {
                for (int k = 1; k < sizeZ - 1; k++)
                {
                    f[0, j, k] = typeIndex == 1 ? -f[1, j, k] : f[1, j, k];
                    f[sizeX - 1, j, k] = typeIndex == 1 ? -f[sizeX - 2, j, k] : f[sizeX - 2, j, k];
                }
            }

            // Explicitly set the 8 corners by average adjacent points
            f[0, 0, 0] = (f[1, 0, 0] + f[0, 1, 0] + f[0, 0, 1]) * 0.33333;
            f[sizeX - 1, 0, 0] = (f[sizeX - 2, 0, 0] + f[sizeX - 1, 1, 0] + f[sizeX - 1, 0, 1]) * 0.33333;
            f[sizeX - 1, sizeY - 1, 0] = (f[sizeX - 2, sizeY - 1, 0] + f[sizeX - 1, sizeY - 2, 0] + f[sizeX - 1, sizeY - 1, 1]) * 0.33333;
            f[sizeX - 1, sizeY - 1, sizeZ - 1] = (f[sizeX - 2, sizeY - 1, sizeZ - 1] + f[sizeX - 1, sizeY - 2, sizeZ - 1] + f[sizeX - 1, sizeY - 1, sizeZ - 2]) * 0.33333;
            f[sizeX - 1, 0, sizeZ - 1] = (f[sizeX - 2, 0, sizeZ - 1] + f[sizeX - 1, 1, sizeZ - 1] + f[sizeX - 1, 0, sizeZ - 2]) * 0.33333;
            f[0, sizeY - 1, 0] = (f[1, sizeY - 1, 0] + f[0, sizeY - 2, 0] + f[0, sizeY - 1, 1]) * 0.33333;
            f[0, sizeY - 1, sizeZ - 1] = (f[1, sizeY - 1, sizeZ - 1] + f[0, sizeY - 2, sizeZ - 1] + f[0, sizeY - 1, sizeZ - 2]) * 0.33333;
            f[0, 0, sizeZ - 1] = (f[1, 0, sizeZ - 1] + f[0, 1, sizeZ - 1] + f[0, 0, sizeZ - 2]) * 0.33333;
        }

        private void Diffuse(byte typeIndex, double[,,] f, double[,,] f0, double rate)
        {
            double a = dt * rate * sizeX * sizeY * sizeZ;
            for (int n = 0; n < 20; n++)
            {
                for (int i = 1; i < sizeX - 1; i++)
                {
                    for (int j = 1; j < sizeY - 1; j++)
                    {
                        for (int k = 1; k < sizeZ - 1; k++)
                        {
                            f[i, j, k] = (f0[i, j, k] + a * (f[i - 1, j, k] + f[i + 1, j, k] +
                                                        f[i, j - 1, k] + f[i, j + 1, k] +
                                                        f[i, j, k - 1] + f[i, j, k + 1])) / (1 + 6 * a);
                        }
                    }
                }
                SetBoundary(typeIndex, f);
            }
        }

        //Core Method
        //This method is to advect the physics value through the velocity field
        //d is the target field, d0 is the current field
        private void Advect(byte typeIndex, double[,,] d, double[,,] d0, double[,,] u, double[,,] v, double[,,] w)
        {
            double dt0 = dt * sizeX;

            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    for (int k = 1; k < sizeZ - 1; k++)
                    {
                        double x = i - dt0 * u[i, j, k];
                        double y = j - dt0 * v[i, j, k];
                        double z = k - dt0 * w[i, j, k];

                        //ensure the point within the boundary
                        x = Math.Max(0.5, Math.Min(sizeX - 1.5, x));
                        y = Math.Max(0.5, Math.Min(sizeY - 1.5, y));
                        z = Math.Max(0.5, Math.Min(sizeZ - 1.5, z));

                        //Find points surrounding
                        int i0 = (int)x;
                        int i1 = i0 + 1;
                        int j0 = (int)y;
                        int j1 = j0 + 1;
                        int k0 = (int)z;
                        int k1 = k0 + 1;

                        //Calculate the interpolation weight
                        double s1 = x - i0;
                        double s0 = 1 - s1;
                        double t1 = y - j0;
                        double t0 = 1 - t1;
                        double r1 = z - k0;
                        double r0 = 1 - r1;

                        // Get new value
                        d[i, j, k] = r0 * (s0 * (t0 * d0[i0, j0, k0] + t1 * d0[i0, j1, k0]) +
                                        s1 * (t0 * d0[i1, j0, k0] + t1 * d0[i1, j1, k0])) +
                                    r1 * (s0 * (t0 * d0[i0, j0, k1] + t1 * d0[i0, j1, k1]) +
                                        s1 * (t0 * d0[i1, j0, k1] + t1 * d0[i1, j1, k1]));
                    }
                }
            }
            SetBoundary(typeIndex, d);
        }

        //Project is to make sure there is no divergency in the velocity field
        private void Project(double[,,] u, double[,,] v, double[,,] w, double[,,] p, double[,,] div)
        {
            //calculate the div of velocity field
            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    for (int k = 1; k < sizeZ - 1; k++)
                    {
                        div[i, j, k] = -0.5 * ((u[i + 1, j, k] - u[i - 1, j, k]) / sizeX +
                                        (v[i, j + 1, k] - v[i, j - 1, k]) / sizeY +
                                        (w[i, j, k + 1] - w[i, j, k - 1]) / sizeZ);
                        p[i, j, k] = 0;
                    }
                }
            }

            SetBoundary(0, div);
            SetBoundary(0, p);

            //solve Poisson equation
            for (int n = 0; n < 20; n++)
            {
                for (int i = 1; i < sizeX - 1; i++)
                {
                    for (int j = 1; j < sizeY - 1; j++)
                    {
                        for (int k = 1; k < sizeZ - 1; k++)
                        {
                            p[i, j, k] = (div[i, j, k] + p[i - 1, j, k] + p[i + 1, j, k] +
                                                    p[i, j - 1, k] + p[i, j + 1, k] +
                                                    p[i, j, k - 1] + p[i, j, k + 1]) / 6;
                        }
                    }
                }
            }
            SetBoundary(0, p);

            //Use the Force delta to adjust the velocity field
            for (int i = 1; i < sizeX - 1; i++)
            {
                for (int j = 1; j < sizeY - 1; j++)
                {
                    for (int k = 1; k < sizeZ - 1; k++)
                    {
                        u[i, j, k] -= 0.5 * sizeX * (p[i + 1, j, k] - p[i - 1, j, k]);
                        v[i, j, k] -= 0.5 * sizeY * (p[i, j + 1, k] - p[i, j - 1, k]);
                        w[i, j, k] -= 0.5 * sizeZ * (p[i, j, k + 1] - p[i, j, k - 1]);
                    }

                }
            }
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
            Swap(ref u, ref u_prev);
            Swap(ref v, ref v_prev);
            Swap(ref w, ref w_prev);

            Diffuse(1, u, u_prev, visc);
            Diffuse(2, v, v_prev, visc);
            Diffuse(3, w, w_prev, visc);

            // Project to make sure div is 0
            Project(u, v, w, pressure, divergency);

            // Swap velocity
            Swap(ref u, ref u_prev);
            Swap(ref v, ref v_prev);
            Swap(ref w, ref w_prev);

            // Advect - fluid transit itself 
            Advect(1, u, u_prev, u_prev, v_prev, w_prev);
            Advect(2, v, v_prev, u_prev, v_prev, w_prev);
            Advect(3, w, w_prev, u_prev, v_prev, w_prev);

            // Project to make sure div is 0
            Project(u, v, w, pressure, divergency);
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
            Advect(0, density, density_prev, u, v, w);
        }

        private void Swap(ref double[,,] a, ref double[,,] b)
        {
            double[,,] temp = a;
            a = b;
            b = temp;
        }

        public double[,,] GetDensity()
        {
            return density;
        }

        public void DrawVector(List<Line> lns)
        {
            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    for (int k = 0; k < sizeZ; k++)
                    {
                        vecs[i, j, k] = new Vector3d(u[i, j, k], v[i, j, k], w[i, j, k]);
                        Line ln = new Line(new Point3d(i, j, k), vecs[i, j, k]);
                        lns.Add(ln);
                    }
                }
            }
        }

        public int Clamp(int a, int b, int c)
        {
            if (a < b) return b;
            if (a > c) return c;
            return a;
        }
    }
}

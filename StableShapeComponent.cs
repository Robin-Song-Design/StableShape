using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Net;
using StableShapeGPU.Properties;

namespace StableShapeGPU
{
    public class StableShapeComponent : GH_Component
    {
        //static variables
        private static bool init = true;
        private static StableFluid3D sf3;
        private static GpuMeshSolver meshSolver;
        private static List<Line> lns = new List<Line>();

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        /// 

        public StableShapeComponent()
          : base("StableFluidSolver", "StableFluidSolver",
            "Main Solver for the Sable Fluid",
            "StableShapeGPU", "Solver")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Size3D", "S", "An int list to store the size of grids.",GH_ParamAccess.list);
            pManager.AddNumberParameter("Diffusion Rate", "Di", "Diffusion Rate of Fluid", GH_ParamAccess.item, 0.0001);
            pManager.AddNumberParameter("Viscocity Rate", "Vi", "Viscocity Rate of Fluid", GH_ParamAccess.item, 0.0001);
            pManager.AddLineParameter("Forces", "F", "A list of lines represent the forces", GH_ParamAccess.list);
            pManager.AddPointParameter("Dots", "D", "A list of Density Dots", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Reset", "Re", "Reset the system", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("ShowVelocity","SV", "Show Velocity Field", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("Mesh", "M", "Mesh to be processed", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines to display the velocities", GH_ParamAccess.list);
            //pManager.AddNumberParameter("Density Field", "Density Field", "A list of number represent the density field", GH_ParamAccess.list);
            pManager.AddMeshParameter("Mesh", "M", "Mesh Processed", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<int> size = new List<int>();
            double diffusion = 0;
            double viscocity = 0;
            List<Line> forces = new List<Line>();
            List<Point3d> dots = new List<Point3d>();
            bool ShowVelocityField = false;
            Mesh mesh = new Mesh();
            bool reset = false;

            if (!DA.GetDataList(0, size)) return;
            DA.GetData(1, ref diffusion);
            DA.GetData(2, ref viscocity);
            DA.GetDataList(3, forces);
            DA.GetDataList(4, dots);
            DA.GetData(5, ref reset);
            DA.GetData(6, ref ShowVelocityField);
            if (!DA.GetData(7, ref mesh)) return;


            if (reset || init)
            {
                reset = false;
                init = false;
                sf3 = new StableFluid3D(size[0], size[1], size[2], 0.1f, (float)diffusion, (float)viscocity);
                meshSolver = new GpuMeshSolver(mesh);
                if (forces.Count > 0 && dots.Count > 0)
                {
                    sf3.AddDot(dots, 10f);
                    sf3.AddForces(forces);
                }
            }

            lns.Clear();
            sf3.AddVelocity();
            sf3.Update();

            for(int m = 0; m < 10; m++)
            {
                meshSolver.SimulateFrame(sf3);
                Mesh remesh = meshSolver.ReconstructMesh();
                DA.SetData(1, remesh);
            }

            if (ShowVelocityField)
            {
                lns = sf3.DrawVector();
            }

            //float[,,] density = sf3.GetDensity();
            //Vector3d[,,]velocity = sf3.GetVelocityField();

            DA.SetDataList(0, lns);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.Fluid;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("BF74E99E-A87B-4B74-9A87-0AECD3391ACD");
    }
}
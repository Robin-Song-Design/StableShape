using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Net;

namespace StableShape.Properties
{
    public class StableSolver2D : GH_Component
    {
        //static variables
        private static bool init = true;
        private static StableFluid2D sf2;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        /// 

        public StableSolver2D()
          : base("StableFluidSolver2D", "StableFluidSolver2D",
            "Main Solver for the Sable Fluid 2D",
            "StableShape", "Solver")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Size2D", "Size2D", "An int list to store the size of grids.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Diffusion Rate", "Diffusion", "Diffusion Rate of Fluid", GH_ParamAccess.item, 0.0001);
            pManager.AddNumberParameter("Viscocity Rate", "Viscocity", "Viscocity Rate of Fluid", GH_ParamAccess.item, 0.0001);
            pManager.AddLineParameter("Forces", "Forces", "A list of lines represent the forces", GH_ParamAccess.list);
            pManager.AddPointParameter("Dots", "Dots", "A list of Density Dots", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Reset", "Reset", "Reset the system", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Display Lines", "Display Lines", "Lines to display the velocities", GH_ParamAccess.list);
            pManager.AddNumberParameter("Density Field", "Density Field", "A list of number represent the density field", GH_ParamAccess.list);
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
            bool reset = false;

            if (!DA.GetDataList(0, size)) return;
            DA.GetData(1, ref diffusion);
            DA.GetData(2, ref viscocity);
            if (!DA.GetDataList(3, forces)) { forces = new List<Line>(); }
            if (!DA.GetDataList(4, dots)) { dots = new List<Point3d>(); }
            DA.GetData(5, ref reset);


            if (reset || init)
            {
                reset = false;
                init = false;
                sf2 = new StableFluid2D(size[0], size[1], 0.1, diffusion, viscocity);
                if (dots.Count > 0)
                {
                    sf2.AddDot(dots, 10);
                }
            }

            sf2.AddForce(forces);
            sf2.Update();

            List<Line> lns = sf2.DrawVector();
            double[,] density = sf2.GetDensity();

            DA.SetDataList(0, lns);
            DA.SetDataList(1, density);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.Fluid2d;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("809B7CAC-E9E4-4FD2-9FA9-69E2F7E203B0");
    }
}
using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using StableShapeGPU;
using StableShapeGPU.Properties;

namespace StableShapeGPU
{
    public class ScreenPoints : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public ScreenPoints()
          : base("ScreenPoints", "SP",
              "Screen a variable rate of points",
              "StableShapeGPU", "Postprocess")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points to be screened", GH_ParamAccess.list);
            pManager.AddNumberParameter("Density", "D", "Density field", GH_ParamAccess.list);
            pManager.AddNumberParameter("Rate", "R", "Rate of the selection", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Screened Poitns", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> points = new List<Point3d>();
            List<double> density = new List<double>();
            double rate = 0.0;

            DA.GetDataList(0, points);
            DA.GetDataList(1, density);
            DA.GetData(2, ref rate);

            int index = (int)(rate * points.Count);

            List<double> values = new List<double>(density);
            values.Sort();
            values.Reverse();
            double standard = values[index];

            List<Point3d> screen_pts = new List<Point3d>();

            for (int i = 0; i < density.Count; i++)
            {
                if (density[i] > standard)
                {
                    screen_pts.Add(points[i]);
                }
            }

            DA.SetDataList(0, screen_pts);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.points;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("0F6D1145-0907-48AA-95B2-AC8915C67CC9"); }
        }
    }
}
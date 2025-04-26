using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace StableShape.Properties
{
    public class SequentialForce : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SequentialForce()
          : base("SequentialForce", "SF",
              "Add forces sequentially",
              "StableShape", "Preprocess")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Curves to be processed", GH_ParamAccess.list);
            pManager.AddIntegerParameter("DivideCount", "D", "How many forces you want", GH_ParamAccess.item, 3);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "List of forces as lines", GH_ParamAccess.list);
            pManager.AddPointParameter("Dots", "D", "List of density dots as points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> crvs = new List<Curve>();
            int divideCount = 0;

            DA.GetDataList(0, crvs);
            DA.GetData(1, ref divideCount);

            List<Line> lns = new List<Line>();
            List<Point3d> pts = new List<Point3d>();
            foreach (Curve curve in crvs)
            {
                for (int i = 0; i < divideCount; i++)
                {
                    var p0 = curve.PointAt(1.0 / divideCount * i);
                    var p1 = curve.PointAt(1.0 / divideCount * (i + 1));
                    Line ln = new Line(p0, p1);
                    lns.Add(ln);
                    pts.Add(p0);
                }
            }

            DA.SetDataList(0, lns);
            DA.SetDataList(1, pts);
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
                return Resources.SF;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("1A5B51F0-C387-4518-B7FF-D6EE303D2255"); }
        }
    }
}
using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace StableShape.Properties
{
    public class Grid : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public Grid()
          : base("Grid", "Grid",
              "Construct Fluid Grid",
              "StableShape", "Preprocess")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("NumX ", "X", "num of X", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("NumY ", "Y", "num of Y", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("NumZ ", "Z", "num of Z", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Point", "P", "Points of the grids", GH_ParamAccess.list);
            pManager.AddNumberParameter("Size", "S", "Resolution of the grids", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int gridx = 0;
            int gridy = 0;
            int gridz = 0;

            DA.GetData(0, ref gridx);
            DA.GetData(1, ref gridy);
            DA.GetData(2, ref gridz);

            List<Point3d> pts = new List<Point3d>();
            List<int> size = new List<int>();

            if (gridz != 0)
            {
                size.Add(gridx);
                size.Add(gridy);
                size.Add(gridz);

                for (int i = 0; i < gridx; i++)
                {
                    for (int j = 0; j < gridy; j++)
                    {
                        for (int k = 0; k < gridz; k++)
                        {
                            pts.Add(new Point3d(i + 0.5, j + 0.5, k + 0.5));
                        }
                    }
                }
            }
            else
            {
                size.Add(gridx);
                size.Add(gridy);

                for (int i = 0; i < gridx; i++)
                {
                    for (int j = 0; j < gridy; j++)
                    {
                        pts.Add(new Point3d(i + 0.5, j + 0.5, 0));
                    }
                }
            }
                
            DA.SetDataList(0, pts);
            DA.SetDataList(1, size);
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
                return Resources.grid;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("AACD2FD5-7C0D-4969-B0BB-11D30CC48CA9"); }
        }
    }
}
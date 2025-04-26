using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Render.ChangeQueue;

namespace StableShape.Properties
{
    public class MeshSolver : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent3 class.
        /// </summary>
        public MeshSolver()
          : base("MeshSolver", "MeshSolver",
              "Process mesh with velocity field and display",
              "StableShape", "Solver")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Size3D", "S", "Resolusion of grids", GH_ParamAccess.list);
            pManager.AddMeshParameter("Mesh", "M", "Mesh to be Processed", GH_ParamAccess.item);
            pManager.AddVectorParameter("VelocityField", "VF", "Velocity Field as list of vectors", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Reset", "R", "", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh Processed", GH_ParamAccess.item);
        }

        ///static
        internal static bool init = true;

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<int> size3d = new List<int>();
            Rhino.Geometry.Mesh mesh = new Rhino.Geometry.Mesh();
            List<Vector3d> velocityField = new List<Vector3d>();
            bool reset = false;

            DA.GetDataList(0, size3d);
            DA.GetData(1, ref mesh);
            DA.GetDataList(2, velocityField);
            DA.GetData(3, ref reset);
            
            
            if (reset || init)
            {
                MeshEditor.springs.Clear();
                MeshEditor.MeshProcess(mesh);
                init = false;
                reset = false;
            }

            VelocityField VF = new VelocityField(size3d, velocityField);

            for (int m = 0; m < 10; m++)
            {
                foreach (var ptc in MeshEditor.particles)
                {
                    if (ptc.forcecounter > 0)
                    {
                        ptc.acceleration *= 1.0 / ptc.forcecounter;
                    }
                }

                foreach (var ptc in MeshEditor.particles)
                {
                    Point3d p = new Point3d(ptc.position);
                    Vector3d velocity = VF.GetVelocityAt(p);
                    ptc.velocity = velocity;
                }

                for (int i = 0; i < MeshEditor.particles.Length; i++)
                {
                    MeshEditor.particles[i].Move();
                }

                MeshEditor.ApplyTension();

                for (int i = 0; i < MeshEditor.particles.Length; i++)
                {
                    MeshEditor.particles[i].Move();
                }

                var remesh = MeshEditor.ReconstructMesh();
                DA.SetData(0,remesh);
            }

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
                return Resources.mesh;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("042E01CF-7168-439D-9773-8966265BEBBA"); }
        }
    }
}
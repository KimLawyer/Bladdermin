using g4;
using gs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.Types;

namespace BladderMin
{
    public static class MeshHelper
    {
        //the Boolean operation
        private static DMesh3 BooleanSubtraction_backup(DMesh3 mesh1, DMesh3 mesh2)
        {
            BoundedImplicitFunction3d meshA = meshToImplicitF(mesh1, 64, 0.2f);
            BoundedImplicitFunction3d meshB = meshToImplicitF(mesh2, 64, 0.2f);

            //take the difference of the bolus mesh minus the tools
            ImplicitDifference3d mesh = new ImplicitDifference3d() { A = meshA, B = meshB };

            //calculate the boolean mesh
            MarchingCubes c = new MarchingCubes();
            c.Implicit = mesh;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;
            c.Bounds = mesh.Bounds();
            c.CubeSize = c.Bounds.MaxDim / 128;
            c.Bounds.Expand(3 * c.CubeSize);
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);

            int triangleCount = c.Mesh.TriangleCount / 2;
            Reducer r = new Reducer(c.Mesh);
            r.ReduceToTriangleCount(triangleCount);
            return c.Mesh;
        }

        private static DMesh3 BooleanSubtraction(DMesh3 mesh1, DMesh3 mesh2, int resolution = 64)
        {
            BoundedImplicitFunction3d meshA = meshToImplicitF(mesh1, resolution, 0.2f);
            BoundedImplicitFunction3d meshB = meshToImplicitF(mesh2, resolution, 0.2f);

            //take the difference of the bolus mesh minus the tools
            ImplicitDifference3d mesh = new ImplicitDifference3d() { A = meshA, B = meshB };

            //calculate the boolean mesh
            MarchingCubes c = new MarchingCubes();
            c.Implicit = mesh;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;
            c.Bounds = mesh.Bounds();
            c.CubeSize = c.Bounds.MaxDim / 128;
            c.Bounds.Expand(3 * c.CubeSize);
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);

            return c.Mesh;
        }

        private static DMesh3 BooleanUnion(DMesh3 mesh1, DMesh3 mesh2)
        {
            BoundedImplicitFunction3d meshA = meshToImplicitF(mesh1, 64, 0.2f);
            BoundedImplicitFunction3d meshB = meshToImplicitF(mesh2, 64, 0.2f);

            //take the difference of the bolus mesh minus the tools
            ImplicitUnion3d mesh = new ImplicitUnion3d() { A = meshA, B = meshB };

            //calculate the boolean mesh
            MarchingCubes c = new MarchingCubes();
            c.Implicit = mesh;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;
            c.Bounds = mesh.Bounds();
            c.CubeSize = c.Bounds.MaxDim / 128;
            c.Bounds.Expand(3 * c.CubeSize);
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);

            int triangleCount = c.Mesh.TriangleCount / 2;
            Reducer r = new Reducer(c.Mesh);
            r.ReduceToTriangleCount(triangleCount);
            return c.Mesh;
        }

        private static DMesh3 BooleanIntersection(DMesh3 mesh1, DMesh3 mesh2)
        {
            BoundedImplicitFunction3d meshA = meshToImplicitF(mesh1, 64, 0.2f);
            BoundedImplicitFunction3d meshB = meshToImplicitF(mesh2, 64, 0.2f);

            //take the intersection of the meshes minus the tools
            ImplicitIntersection3d mesh = new ImplicitIntersection3d() { A = meshA, B = meshB };

            //calculate the boolean mesh
            MarchingCubes c = new MarchingCubes();
            c.Implicit = mesh;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;
            c.Bounds = mesh.Bounds();
            c.CubeSize = c.Bounds.MaxDim / 128;
            c.Bounds.Expand(3 * c.CubeSize);
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);

            int triangleCount = c.Mesh.TriangleCount / 2;
            Reducer r = new Reducer(c.Mesh);
            r.ReduceToTriangleCount(triangleCount);
            return c.Mesh;
        }

        private static bool Intersects(BoundedImplicitFunction3d meshA, BoundedImplicitFunction3d meshB)
        {
            //take the intersection of the meshes minus the tools
            ImplicitIntersection3d mesh = new ImplicitIntersection3d() { A = meshA, B = meshB };
            if (mesh.Bounds().Width > 0)
            {
                //intersection
                return true;
            }
            else
            {
                //no intersection
                return false;
            }
        }

        //used for Boolean calculations
        static Func<DMesh3, int, double, BoundedImplicitFunction3d> meshToImplicitF = (meshIn, numcells, max_offset) =>
        {
            //double meshCellSize = meshIn.CachedBounds.MaxDim / numcells;
            double meshCellSize = meshIn.CachedBounds.MaxDim / numcells;
            MeshSignedDistanceGrid levelSet = new MeshSignedDistanceGrid(meshIn, meshCellSize);
            levelSet.ExactBandWidth = (int)(max_offset / meshCellSize) + 1;
            levelSet.Compute();
            return new DenseGridTrilinearImplicit(levelSet.Grid, levelSet.GridOrigin, levelSet.CellSize);
        };

        //convert a MeshGeometry3D object into a DMesh object
        public static DMesh3 MeshGeometryToDMesh(MeshGeometry3D mesh)
        {
            List<Vector3d> vertices = new List<Vector3d>();
            foreach (Point3D point in mesh.Positions)
                vertices.Add(new Vector3d(point.X, point.Y, point.Z));

            List<Vector3f> normals = new List<Vector3f>();
            foreach (Point3D normal in mesh.Normals)
                normals.Add(new Vector3f(normal.X, normal.Y, normal.Z));

            if (normals.Count() == 0)
                normals = null;

            List<Index3i> triangles = new List<Index3i>();
            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
                triangles.Add(new Index3i(mesh.TriangleIndices[i], mesh.TriangleIndices[i + 1], mesh.TriangleIndices[i + 2]));

            //converting the meshes to use Implicit Surface Modeling
            return DMesh3Builder.Build(vertices, triangles, normals);
        }

        public static List<Tuple<double, VMS.TPS.Common.Model.Types.VVector>> Volumes(MeshGeometry3D mesh)
        {
            DMesh3 m3 = MeshGeometryToDMesh(mesh);
            DMesh3[] mC = MeshConnectedComponents.Separate(m3);
            List<Tuple<double, VMS.TPS.Common.Model.Types.VVector>> ListVol = new List<Tuple<double, VMS.TPS.Common.Model.Types.VVector>>();
            foreach (DMesh3 m in mC)
            {
                var triangles = m.TriangleIndices();
                Func<int, Vector3d> getVertexF = (a) =>
                {
                    Vector3d V = m.GetVertex(a);
                    return V;
                };
                var Cen = MeshMeasurements.Centroid(m.Vertices());
                var RepairHandler = new gs.MeshAutoRepair(m);
                RepairHandler.Apply();  // attempt to close mesh
                if (m.IsClosed())
                {
                    Vector2d Vol = MeshMeasurements.VolumeArea(m, triangles, getVertexF);
                    var result = new Tuple<double, VMS.TPS.Common.Model.Types.VVector>(Vol.x / 1000, new VMS.TPS.Common.Model.Types.VVector(Cen.x, Cen.y, Cen.z));
                    ListVol.Add(result);
                }
            }
            return ListVol;
        }
        /// <summary>
        /// Converts a DMesh3 object to a MeshGeometry3D object
        /// DMesh3 is used within the Bolusmesh class to perform calculations
        /// MeshGeometry3D is used by helix Toolkit to display the mesh
        /// This will not calculate normals
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MeshGeometry3D DMeshToMeshGeometry(DMesh3 value)
        {
            if (value != null)
            {
                //compacting the DMesh to the indices are true
                var mesh_copy = new DMesh3(value, true);
                MeshGeometry3D mesh = new MeshGeometry3D();

                //calculate positions
                var vertices = value.Vertices();
                foreach (var vert in vertices)
                    mesh.Positions.Add(new Point3D(vert.x, vert.y, vert.z));

                //calculate faces
                var vID = value.VertexIndices().ToArray();
                var faces = value.Triangles();
                foreach (Index3i f in faces)
                {
                    mesh.TriangleIndices.Add(Array.IndexOf(vID, f.a));
                    mesh.TriangleIndices.Add(Array.IndexOf(vID, f.b));
                    mesh.TriangleIndices.Add(Array.IndexOf(vID, f.c));
                }

                return mesh;
            }
            else
                return null;
        }

        public static MeshGeometry3D Sub(MeshGeometry3D obj1, MeshGeometry3D obj2)
        {
            DMesh3 mesh1 = MeshGeometryToDMesh(obj1);
            DMesh3 mesh2 = MeshGeometryToDMesh(obj1);
            DMesh3 sub = BooleanSubtraction(mesh1, mesh2);
            return DMeshToMeshGeometry(sub);
        }
        public static MeshGeometry3D Union(MeshGeometry3D obj1, MeshGeometry3D obj2)
        {
            DMesh3 mesh1 = MeshGeometryToDMesh(obj1);
            DMesh3 mesh2 = MeshGeometryToDMesh(obj1);
            DMesh3 sub = BooleanUnion(mesh1, mesh2);
            return DMeshToMeshGeometry(sub);
        }
        public static MeshGeometry3D Intersection(MeshGeometry3D obj1, MeshGeometry3D obj2)
        {
            DMesh3 mesh1 = MeshGeometryToDMesh(obj1);
            DMesh3 mesh2 = MeshGeometryToDMesh(obj1);
            DMesh3 sub = BooleanIntersection(mesh1, mesh2);
            return DMeshToMeshGeometry(sub);
        }

        public static MeshGeometry3D SimplifyMesh(MeshGeometry3D mesh3D, int reductionFactor = 10)
        {
            var dMesh3D = MeshGeometryToDMesh(mesh3D);
            var red = new Reducer(dMesh3D);
            red.ReduceToTriangleCount(dMesh3D.TriangleCount / reductionFactor);
            return DMeshToMeshGeometry(dMesh3D);
        }


        public static double GetVolume(MeshGeometry3D obj)
        {
            DMesh3 mesh = MeshGeometryToDMesh(obj);
            var triangles = mesh.TriangleIndices();
            Func<int, Vector3d> getVertexF = (a) =>
            {
                Vector3d V = mesh.GetVertex(a);
                return V;
            };
            Vector2d Vol = MeshMeasurements.VolumeArea(mesh, triangles, getVertexF);
            return Vol.x / 1000;
        }

        public static double GetVolume(DMesh3 mesh)
        {
            var triangles = mesh.TriangleIndices();
            Func<int, Vector3d> getVertexF = (a) =>
            {
                Vector3d V = mesh.GetVertex(a);
                return V;
            };
            Vector2d Vol = MeshMeasurements.VolumeArea(mesh, triangles, getVertexF);
            return Vol.x / 1000;
        }
        public static double GetOverlapVolume(MeshGeometry3D obj1, MeshGeometry3D obj2)
        {
            DMesh3 mesh1 = MeshGeometryToDMesh(obj1);
            DMesh3 mesh2 = MeshGeometryToDMesh(obj2);
            DMesh3 MI = BooleanIntersection(mesh1, mesh2);
            var triangles = MI.TriangleIndices();
            Func<int, Vector3d> getVertexF = (a) =>
            {
                Vector3d V = MI.GetVertex(a);
                return V;
            };
            Vector2d Vol = MeshMeasurements.VolumeArea(MI, triangles, getVertexF);
            return Vol.x / 1000;
        }

        private static DMesh3 generatMeshF(BoundedImplicitFunction3d root, int numcells)
        {
            MarchingCubes c = new MarchingCubes();
            c.Implicit = root;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;      // cube-edge convergence method
            c.RootModeSteps = 5;                                        // number of iterations
            c.Bounds = root.Bounds();
            c.CubeSize = c.Bounds.MaxDim / numcells;
            c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);                           // generate normals

            // cleanup
            MeshAutoRepair repair = new MeshAutoRepair(c.Mesh);
            repair.Apply();

            return repair.Mesh;
        }
        public static DMesh3 OffsetMesh(DMesh3 mesh, double offset, int resolution = 64)
        {
            BoundedImplicitFunction3d meshImplicit = meshToImplicitF(mesh, resolution, 0.2f);
            return generatMeshF(new ImplicitOffset3d() { A = meshImplicit, Offset = offset }, resolution);
        }

        public static DMesh3 PrecisionCrop(DMesh3 meshIn, MeshGeometry3D cropMesh, double crop)
        {
            int resolution = crop < 2 ? 128 : 64; // adjust resolution based on crop size
            BoundedImplicitFunction3d meshImplicit = meshToImplicitF(meshIn, resolution, 0.2f);
            BoundedImplicitFunction3d cropMeshImplicit = meshToImplicitF(MeshGeometryToDMesh(cropMesh), resolution, 0.2f);
            var offsetMeshImplicit = new ImplicitOffset3d() { A = cropMeshImplicit, Offset = crop };
            if (Intersects(meshImplicit, offsetMeshImplicit) == false)
            {
                //no intersection, return the original mesh
                return meshIn;
            }
            else
            {
                ImplicitDifference3d croppedMeshImplicit = new ImplicitDifference3d() { A = meshImplicit, B = offsetMeshImplicit };

                //calculate the resulting DMesh3
                MarchingCubes c = new MarchingCubes();
                c.Implicit = croppedMeshImplicit;
                c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
                c.RootModeSteps = 5;
                c.Bounds = croppedMeshImplicit.Bounds();
                c.CubeSize = c.Bounds.MaxDim / 128;
                c.Bounds.Expand(3 * c.CubeSize);
                c.Generate();
                MeshNormals.QuickCompute(c.Mesh);

                MeshAutoRepair repair = new MeshAutoRepair(c.Mesh);
                repair.Apply();
                return repair.Mesh;
            }

        }

        public static DMesh3 PrecisionCrop2(DMesh3 meshIn, MeshGeometry3D cropMesh, double crop)
        {
            int resolution = crop < 2 ? 128 : 64; // adjust resolution based on crop size
            var offsetMesh = OffsetMesh(MeshGeometryToDMesh(cropMesh), crop, resolution);
            var intersection = BooleanIntersection(MeshGeometryToDMesh(cropMesh), meshIn);
            if (intersection.TriangleCount == 0)
                return meshIn;
            else
            {
                var reducedMesh = BooleanSubtraction(meshIn, offsetMesh);
                return reducedMesh;
            }
        }

        public static DMesh3 InitializeCrop(DMesh3 mesh, MeshGeometry3D cropMesh)
        {
            int resolution = 128; // adjust resolution based on crop size
            DMesh3 offsetMesh = OffsetMesh(MeshGeometryToDMesh(cropMesh), 0, resolution);
            return BooleanSubtraction(mesh, offsetMesh, resolution);
        }

        internal static bool CheckOverlap(DMesh3 subStructureMesh, MeshGeometry3D meshGeometry)
        {
            // check of there is overlap between the meshes
            DMesh3 mesh = MeshGeometryToDMesh(meshGeometry);
            DMesh3 intersection = BooleanIntersection(subStructureMesh, mesh);
            if (intersection.TriangleCount > 0)
            {
                //there is an overlap
                return true;
            }
            else
            {
                //no overlap
                return false;
            }
        }

        public static MeshGeometry3D Smooth(MeshGeometry3D meshIn)
        {

            var mesh1 = MeshGeometryToDMesh(meshIn);
            //take the difference of the bolus mesh minus the tools
            var rm = new Remesher(mesh1);
            rm.PreventNormalFlips = true;
            rm.EnableSmoothing = true;
            rm.SmoothSpeedT = 0.5f;
            for (int i = 0; i < 10; i++)
            {
                rm.BasicRemeshPass();
            }
            return DMeshToMeshGeometry(rm.Mesh);

        }


        /// <summary>
        /// Implementation of Chaikin's algorithm to smooth a path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<VVector> SmoothPath(List<VVector> path)
        {
            var output = new List<VVector>();

            if (path.Count > 0)
            {
                output.Add(path[0]);
            }

            for (var i = 0; i < path.Count - 1; i++)
            {
                var p0 = path[i];
                var p1 = path[i + 1];
                var p0x = p0.x;
                var p0y = p0.y;
                var p1x = p1.x;
                var p1y = p1.y;

                var qx = 0.75f * p0x + 0.25f * p1x;
                var qy = 0.75f * p0y + 0.25f * p1y;
                var Q = new VVector(qx, qy, p0.z);

                var rx = 0.25f * p0x + 0.75f * p1x;
                var ry = 0.25f * p0y + 0.75f * p1y;
                var R = new VVector(rx, ry, p0.z);

                output.Add(Q);
                output.Add(R);
            }

            if (path.Count > 1)
            {
                output.Add(path[path.Count - 1]);
            }

            return output;
        }

    }
}

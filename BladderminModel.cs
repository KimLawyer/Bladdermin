using System;
using Serilog;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Threading;
using g4;

namespace BladderMin
{
    public struct BladderMinMarginResults
    {
        public double SupMargin; // initial margin for superior direction, mm
        public double AntMargin; // initial margin for anterior direction, mm
        public double TotalSupMargin;
        public double TotalAntMargin;
        public double InfMarginSmoothingMargin; // margin used to smooth the inferior margin by expansion then joining with the original structure, mm
    }
    public struct BladderMinMarginInitializationParameters
    {
        public double InitialSupMargin; // initial margin for superior direction, mm
        public double InitialAntMargin; // initial margin for anterior direction, mm
        public double InfMarginSmoothingMargin; // margin used to smooth the inferior margin by expansion then joining with the original structure, mm
        public double SupMarginReductionIncrement; // amount to increase internal sup margin by in each iteration, mm
    }
    public class BladderMinMargins
    {
        private BladderMinMarginInitializationParameters _startParameters;
        private double _currentSupMargin = 0; //mm
        private double _currentAntMargin = 0; //mm
        private double _totalSupMargin = 0; //mm
        private double _totalAntMargin = 0; //mm
        private double _runningSupMargin = 0; //mm
        private double _runningAntMargin = 0; //mm
        private double maxInternalMargin = 50; // mm
        private double supMarginIncrement = 1; // mm

        public BladderMinMargins()
        {
            _startParameters = new BladderMinMarginInitializationParameters
            {
                InitialSupMargin = 0,
                InitialAntMargin = 0,
                InfMarginSmoothingMargin = 25,
                SupMarginReductionIncrement = 1
            };
        }
        public bool IncrementMargins()
        {
            _currentAntMargin = Math.Ceiling((_currentSupMargin / 3));
            _currentSupMargin += supMarginIncrement;
            bool maxInternalMarginReached = _currentSupMargin > maxInternalMargin;
            if (maxInternalMarginReached)
            {
                _currentSupMargin %= (maxInternalMargin + 1); // overflow back to 1 if max internal margin reached
                _currentAntMargin = Math.Ceiling((_currentSupMargin / 3)); // Resets ant margin to correct for redoubling of ant margin during rebasing process. 
                _totalSupMargin += _runningSupMargin; // add running margin to total margin
                _totalAntMargin += _runningAntMargin;
                _runningSupMargin = 0; // reset running margins
                _runningAntMargin = 0;
            }
            _runningSupMargin = _currentSupMargin;
            _runningAntMargin = _currentAntMargin;
            return (maxInternalMarginReached);
        }

        public BladderMinMarginResults GetCurrentMargins()
        {
            return new BladderMinMarginResults()
            {
                SupMargin = _currentSupMargin,
                AntMargin = _currentAntMargin,
                TotalSupMargin = _totalSupMargin + _runningSupMargin,
                TotalAntMargin = _totalAntMargin + _runningAntMargin,
                InfMarginSmoothingMargin = _startParameters.InfMarginSmoothingMargin
            };
        }

        public AxisAlignedMargins GenerateAxisAlignedInnerMargins(PatientOrientation o)
        {
            return Helpers.ConvertInnerMargins(o, StructureMarginGeometry.Inner, 0, _currentAntMargin, 0, 0, 0, _currentSupMargin);
        }

        public AxisAlignedMargins GenerateAxisAlignedInfSmoothingMargins(PatientOrientation o)
        {
            return Helpers.ConvertInnerMargins(o, StructureMarginGeometry.Outer, 0, 0, _startParameters.InfMarginSmoothingMargin, 0, 0, 0);
        }

    }

    public struct BladderMinCreationResults
    {
        public bool Success;
        public string Message;
        public ProtocolResult ProtocolResult;
        public BladderMinMarginResults MarginResult;

        public BladderMinCreationResults(bool success, string message, ProtocolResult protocolResult = new ProtocolResult(), BladderMinMarginResults marginResult = new BladderMinMarginResults())
        {
            Success = success;
            Message = message;
            MarginResult = marginResult;
            ProtocolResult = protocolResult;
        }

    }

    public class BladderminModel
    {
        //Define properties
        private EsapiWorker _ew;
        private BladderMinMarginInitializationParameters _marginParameters { get; set; }

        //BladderminModel constructor
        public BladderminModel(EsapiWorker ew)
        {
            _ew = ew;
            _marginParameters = new BladderMinMarginInitializationParameters
            {
                InitialSupMargin = 0,
                InitialAntMargin = 0,
                InfMarginSmoothingMargin = 25,
                SupMarginReductionIncrement = 1
            };
        }

        public async Task<BladderMinCreationResults> CreateBladderMinStructure(Protocol protocol, string bladderStructureId, string planSumId = "")
        {
            string protocolSelected = protocol.Name + " was selected.";
            SeriLog.AddInfo(protocolSelected);
            SeriLog.AddInfo(protocol.wereNodesTreated);
            BladderMinCreationResults result = new BladderMinCreationResults(false, "Method did not complete");
            await Task.Run(() => _ew.AsyncRun((p, pl) =>
            {
                try
                {
                    //Get bladder structure.
                    Structure bladder = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == bladderStructureId);
                    //Checks that the user selected a bladder contour.
                    if (bladder == null)
                    {
                        var errorMessage = "Could not find bladder contour.";
                        SeriLog.AddError(errorMessage);
                        throw new Exception(errorMessage);
                    }
                    else if (bladder.IsEmpty)
                    {
                        var errorMessage = "Bladder structure is empty.";
                        SeriLog.AddError(errorMessage);
                        throw new Exception(errorMessage);
                    }

                    //Creates a copy of the bladder. Converts the bladderCopy_AUTO to a low resolution structure in the event th
                    SeriLog.AddInfo("Copying bladder structure.");
                    string bladCopyName = "bladCopy_AUTO";
                    Structure bladCopy = pl.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(bladCopyName, StringComparison.OrdinalIgnoreCase));
                    if (bladCopy != null)
                    {
                        string warningMessage = $"{bladCopyName} already exists. Clearing structure...";
                        pl.StructureSet.RemoveStructure(bladCopy); // remove existing low res structure
                        SeriLog.AddWarning(warningMessage);
                    }
                    //If the bladder is high res, convert the bladder copy structure to a low resolution to increase script speed efficiency
                    if (bladder.IsHighResolution)
                    {
                        SeriLog.AddInfo("High res bladder structure detected. Converting the bladder copy to a low res structure.");
                        bladCopy = Helpers.CreateLowResolutionCopy(bladder, bladCopyName, pl.StructureSet); // ensure low res for speed.
                    }
                    else
                    {
                        bladCopy = pl.StructureSet.AddStructure("CONTROL", bladCopyName);
                        bladCopy.SegmentVolume = bladder.SegmentVolume;
                    }

                    //Creates a temporary step 2 bladder structure in situations where the margin reductions are larger > 50mm
                    string BladderMinTest = "Blad_AUTO_test";
                    Structure bladderMinTest = pl.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(BladderMinTest, StringComparison.OrdinalIgnoreCase));
                    if (bladderMinTest != null)
                    {
                        string warningMessage = $"{bladderMinTest} already exists. Structure will be overwritten...";
                        SeriLog.AddWarning(warningMessage);
                    }
                    else
                    {
                        bladderMinTest = pl.StructureSet.AddStructure("CONTROL", BladderMinTest);
                    }
                    bladderMinTest.SegmentVolume = bladCopy.SegmentVolume;

                    //Create Bladdermin_AUTO structure.
                    SeriLog.AddInfo("Creating Bladmin_AUTO structure.");
                    string bladderMinName = "Bladmin_AUTO";
                    var bladderMin = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == bladderMinName);
                    if (bladderMin != null)
                    {
                        string warningMessage = $"{bladderMinName} already exists. Clearing structure...";
                        pl.StructureSet.RemoveStructure(bladderMin);
                        SeriLog.AddWarning(warningMessage);
                    }
                    bladderMin = pl.StructureSet.AddStructure("CONTROL", bladderMinName);
                    bladderMin.SegmentVolume = bladCopy.SegmentVolume;

                    //------------------------------------------------------------------------------------------------------------------
                    // Get planning item from which to evaluate dose. This will be a Plan Sum for a multi phase protocol and a Plan if not;
                    PlanningItem planningItem = null;
                    if (protocol.isMultiPhase)
                    {
                        planningItem = pl.Course.PlanSums.FirstOrDefault(x => x.Id.Equals(planSumId, StringComparison.CurrentCultureIgnoreCase));
                    }
                    else
                        planningItem = pl;

                    if (planningItem == null)
                    {
                        string errorMessage = "Could not find plan or plan sum.";
                        SeriLog.AddError(errorMessage);
                        throw new Exception(errorMessage);
                    }

                    //Define low dose isodose structure that is needed to ensure the bladdermin structure includes low dose in the bladder.
                    Structure lowDoseOverlap = CreateLowDoseIsoStructure(pl.TreatmentOrientation, planningItem, protocol, bladCopy);

                    //------------------------------------------------------------------------------------------------------------------------
                    //Initialize variables for volume reduction loop.
                    double blaMinVol = bladCopy.Volume;

                    BladderMinMargins bladderMinMargins = new BladderMinMargins();

                    bool keepShrinking = true;
                    ProtocolResult lastAcceptableBladderMinResult = new ProtocolResult();
                    BladderMinMarginResults lastAcceptableMarginResult = new BladderMinMarginResults();

                    lastAcceptableBladderMinResult = protocol.EvaluateBladderMin(planningItem, bladderMinTest);
                    lastAcceptableMarginResult = bladderMinMargins.GetCurrentMargins();

                    //---------------------------------------------------------------------------------------------------------------------------------
                    //Initiate volume reduction loop based on volume constraints at each iteration being met.
                    while (keepShrinking)
                    {
                        bladderMin.SegmentVolume = bladderMinTest.SegmentVolume; // Set bladdermin to result of previous successful test;

                        bool internalMarginLimitReached = bladderMinMargins.IncrementMargins(); // Increment margins and determine if internal margin limit has been reached.
                        if (internalMarginLimitReached)
                        {
                            SeriLog.AddWarning("Margin reduction exceeds maximum. Rebasing bladder on existing bladderMin to continue reduction...");
                            bladCopy.SegmentVolume = bladderMinTest.SegmentVolume;
                        }

                        // Reduce the bladder volume by the latest margins;
                        SeriLog.AddInfo("Reducing Bladdermin...");
                        ReduceBladderMin_v2(pl.TreatmentOrientation, bladderMinTest, lowDoseOverlap, bladCopy, bladderMinMargins, pl.StructureSet.Image);

                        // Hack due to Eclipse bug, needed so GetVolumeAtDose works.
                        planningItem.GetDVHCumulativeData(bladderMinTest, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                        // Evaluate whether bladdermin meets protocol constraints. 
                        var protocolResult = protocol.EvaluateBladderMin(planningItem, bladderMinTest);
                        if (protocolResult.IsMet)
                        {
                            SeriLog.AddInfo("Constraints met.");
                        }
                        keepShrinking = protocolResult.IsMet;

                        if (keepShrinking) // include the last acceptable bladdermin reductions in the total margins
                        {
                            lastAcceptableBladderMinResult = protocolResult;
                            lastAcceptableMarginResult = bladderMinMargins.GetCurrentMargins();
                            string currentVolume = "Current Bladdermin volume: " + bladderMin.Volume.ToString("0.00") + "cc";
                            string currentSupMargins = "Current sup margin: " + lastAcceptableMarginResult.TotalSupMargin.ToString();
                            string currentAntMargins = " Current ant margin: " + lastAcceptableMarginResult.TotalAntMargin.ToString();
                            SeriLog.AddInfo(currentSupMargins + currentAntMargins);
                            SeriLog.AddInfo(currentVolume);

                            if (lastAcceptableMarginResult.TotalSupMargin == 150)
                            {
                                SeriLog.AddError("BladderMin creation failed. Please create structure manually");
                                keepShrinking = false;
                            }
                        }
                        else
                        {
                            SeriLog.AddWarning("Constraints have been exceeded. Halting reductions...");
                        }

                    }

                    // Final smoothing
                    SmoothBladderMin(bladderMin, bladCopy); 
                    // Note for Kim - need to regenerate lastAcceptableMarginResult after this smoothing because your view output is bound to a property of this structure.
                    string finalVolume = "Final Bladdermin volume: " + bladderMin.Volume.ToString("0.00") + "cc";
                    string finalSupMargins = "Final sup margin: " + lastAcceptableMarginResult.TotalSupMargin.ToString();
                    string finalAntMargins = " Final ant margin: " + lastAcceptableMarginResult.TotalAntMargin.ToString();
                    SeriLog.AddInfo(finalSupMargins + finalAntMargins);
                    SeriLog.AddInfo(finalVolume);
                    //Run the GetDVHCumlativeData and EvaluateBladdermin methods on the final bladderMin structure after smoothing.
                    planningItem.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                    lastAcceptableBladderMinResult = protocol.EvaluateBladderMin(planningItem, bladderMin); 

                    //---------------------------------------------------------------------------------------------------------------
                    //Clean up temp structures that are no longer needed
                    SeriLog.AddInfo("Cleaning up temp structures...");
                    pl.StructureSet.RemoveStructure(bladCopy);
                    pl.StructureSet.RemoveStructure(lowDoseOverlap);
                    pl.StructureSet.RemoveStructure(bladderMinTest);

                    if (lastAcceptableMarginResult.TotalSupMargin == 150)
                    {
                        result = new BladderMinCreationResults(false, "BladderMin creation failed. Please create structure manually", lastAcceptableBladderMinResult, lastAcceptableMarginResult);
                        pl.StructureSet.RemoveStructure(bladderMin);
                    }
                    else
                    {
                        result = new BladderMinCreationResults(true, "Complete", lastAcceptableBladderMinResult, lastAcceptableMarginResult);
                    }

                }
                catch (Exception ex)
                {
                    result = new BladderMinCreationResults(false, ex.Message);
                }
            }));
            return result;
        }

        private void SmoothBladderMin(Structure bladderMin, Structure bladder)
        {
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Margin(40);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Margin(-40);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(bladder.SegmentVolume);
        }

        public Structure CreateLowDoseIsoStructure(PatientOrientation orientation, PlanningItem doseSource, Protocol protocol, Structure bladder)
        {
            SeriLog.AddInfo("Creating low dose isodose structure...");
            Structure lowDoseIso;
            string lowDoseIsoId = "lowDoseIso";
            lowDoseIso = doseSource.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(lowDoseIsoId, StringComparison.OrdinalIgnoreCase));
            if (lowDoseIso != null)
            {
                string warningMessage = $"{lowDoseIsoId} already exists. Clearing structure...";
                SeriLog.AddWarning(warningMessage);
            }
            else
            {
                lowDoseIso = doseSource.StructureSet.AddStructure("CONTROL", lowDoseIsoId);
            }
            if (doseSource.DoseValuePresentation == DoseValuePresentation.Absolute)
            {
                lowDoseIso.ConvertDoseLevelToStructure(doseSource.Dose, protocol.LowDoseConstraintValue);
            }
            else
            {
                lowDoseIso.ConvertDoseLevelToStructure(doseSource.Dose, new DoseValue(protocol.LowDoseConstraintValue / ((PlanSetup)doseSource).TotalDose * 100, DoseValue.DoseUnit.Percent));
            }
           
            //Smooth low dose structure
            SeriLog.AddInfo("Smoothing low dose isodose structure...");
            var aaSmoothOut = Helpers.ConvertInnerMargins(orientation, StructureMarginGeometry.Outer, 20, 20, 20, 20, 20, 20);
            var aaSmoothIn = Helpers.ConvertInnerMargins(orientation, StructureMarginGeometry.Inner, 20, 20, 20, 20, 20, 20);
            Thread.Sleep(1000);
            lowDoseIso.SegmentVolume = lowDoseIso.SegmentVolume.AsymmetricMargin(aaSmoothOut);
            Thread.Sleep(300);
            lowDoseIso.SegmentVolume = lowDoseIso.SegmentVolume.AsymmetricMargin(aaSmoothIn);
            Thread.Sleep(300);
            lowDoseIso.SegmentVolume = lowDoseIso.SegmentVolume.And(bladder.SegmentVolume);
            SeriLog.AddInfo("Low dose isodose structure completed.");
            return lowDoseIso;
        }

        private void ReduceBladderMin(PatientOrientation orientation, Structure bladderMin, Structure lowDoseOverlap, Structure currentBladder, BladderMinMargins margins)
        {
            //Get margins with respect to patient orientation and perform the volume reduction
            AxisAlignedMargins aaMargins = margins.GenerateAxisAlignedInnerMargins(orientation);

            bladderMin.SegmentVolume = currentBladder.SegmentVolume.AsymmetricMargin(aaMargins);

            //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
            AxisAlignedMargins smoothingMargins = margins.GenerateAxisAlignedInfSmoothingMargins(orientation);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(smoothingMargins);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(currentBladder);

            //Find overlap of bladder and low dose structure and then OR it with the bladdermin to add that back to the bladdermin structure. 
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(currentBladder.SegmentVolume.And(lowDoseOverlap));
        }

        private void ReduceBladderMin_v2(PatientOrientation orientation, Structure bladderMin, Structure lowDoseOverlap, Structure currentBladder, BladderMinMargins margins, Image image)
        {
            //Get margins with respect to patient orientation and perform the volume reduction
            AxisAlignedMargins aaMargins = margins.GenerateAxisAlignedInnerMargins(orientation);

            bladderMin.SegmentVolume = currentBladder.SegmentVolume.AsymmetricMargin(aaMargins);

            //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
            AxisAlignedMargins smoothingMargins = margins.GenerateAxisAlignedInfSmoothingMargins(orientation);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(smoothingMargins);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(currentBladder);

            //Find overlap of bladder and low dose structure and then add it back to the bladdermin structure up to its sup extent
            var Z1 = bladderMin.MeshGeometry.Bounds.Z;
            var Z2 = bladderMin.MeshGeometry.Bounds.SizeZ + Z1;
            (int startSlice, int endSlice) = GetSlice(Z1, Z2, image);
            int lastSliceWithContour = startSlice;
            for (int slice = startSlice; slice <= endSlice; slice++)
            {
                // Adding the contours of the overlap slices of bladdermin with low dose structure to the bladdermin structure
                var c = bladderMin.GetContoursOnImagePlane(slice);
                if (c.Length > 0)
                {
                    var overlapContours = lowDoseOverlap.GetContoursOnImagePlane(slice);
                    foreach (var contour in overlapContours)
                        bladderMin.AddContourOnImagePlane(contour, slice);
                }
                var bladderContours = bladderMin.GetContoursOnImagePlane(slice);
                if (bladderContours.Length > 0)
                {
                    List<Vector2d> projectedPoints = bladderContours.Aggregate(new List<Vector2d>(), (acc, contour) =>
                    {
                        acc.AddRange(contour.Select(p => new Vector2d(p.x, p.y)));
                        return acc;
                    });
                    ConvexHull2 hull = new ConvexHull2(projectedPoints, 0.1, QueryNumberType.QT_DOUBLE);
                    Polygon2d hullPolygon = hull.GetHullPolygon();
                    var hullVertices = hullPolygon.Vertices.Select(v => new VVector(v.x, v.y, bladderContours.FirstOrDefault().FirstOrDefault().z)).ToList();
                    var smoothHullVertices = MeshHelper.SmoothPath(hullVertices);
                    bladderMin.ClearAllContoursOnImagePlane(slice);
                    bladderMin.AddContourOnImagePlane(smoothHullVertices.ToArray(), slice);
                    lastSliceWithContour = slice; // keep track of the last slice with a contour
                }
            }
            // Smooth top of bladdermin contour by adding a scaled slice to the top based on the last slice
            var topContour = bladderMin.GetContoursOnImagePlane(lastSliceWithContour);
            if (topContour.Length > 0)
            {
                var topPoints = topContour.FirstOrDefault().Select(p => new VVector(p.x, p.y, Z2));
                // shrink the topPoints contour by 25% around its centroid
                var centroid = new VVector(topPoints.Average(p => p.x), topPoints.Average(p => p.y), Z2);
                topPoints = topPoints.Select(p => new VVector(
                    centroid.x + 0.75 * (p.x - centroid.x),
                    centroid.y + 0.75 * (p.y - centroid.y),
                    Z2)).ToList();
                bladderMin.AddContourOnImagePlane(topPoints.ToArray(), lastSliceWithContour + 1); // add a contour on the next slice above the last slice with a contour
                topPoints = topPoints.Select(p => new VVector(
                   centroid.x + 0.5 * (p.x - centroid.x),
                   centroid.y + 0.5 * (p.y - centroid.y),
                   Z2)).ToList();
                bladderMin.AddContourOnImagePlane(topPoints.ToArray(), lastSliceWithContour + 2); // add a contour on the next slice above the last slice with a contour
            }

            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(currentBladder.SegmentVolume); // adjustment margin to remove regions outside of bladder
        }

        private (int, int) GetSlice(double zInf, double zSup, Image image)
        {
            // z is in mm
            double epsilon = 1E-2;
            var imageRes = image.ZRes;
            int startSlice;
            int endSlice;
            if (image.ZDirection.z > 0)
            {
                startSlice = (int)Math.Floor((zInf - epsilon - (image.Origin.z - image.UserOrigin.z)) / imageRes);
                endSlice = (int)Math.Ceiling((zSup + epsilon - (image.Origin.z - image.UserOrigin.z)) / imageRes);
            }
            else
            {
                startSlice = (int)Math.Floor((zInf - epsilon - (image.Origin.z - image.UserOrigin.z)) / imageRes);
                endSlice = (int)Math.Ceiling((zSup + epsilon - (image.Origin.z - image.UserOrigin.z)) / imageRes);
            }
            startSlice = Math.Max(0, startSlice);
            endSlice = Math.Min(image.ZSize - 1, endSlice);
            return (startSlice, endSlice);
        }
    }
}

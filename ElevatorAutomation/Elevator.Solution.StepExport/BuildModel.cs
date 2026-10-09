using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Elevator.Core.Serialization;
using Elevator.Solution.Bridge;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
/// <summary>All dimensions in this source and Parameters.json are millimetres. SOLIDWORKS API geometry uses metres.</summary>
public class BuildModel
{
    static SldWorks sw;
    static ModelDoc2 model;
    static ModelDoc2 construction;
    static PartDoc part;
    static StepModelParameters p;
    static string partTemplate;
    static string previousActiveTitle;
    static bool ownsApplication;
    static List<BodyRecord> records = new List<BodyRecord>();
    static List<Body2> solids = new List<Body2>();
    static Elevator.Core.Serialization.IJsonSerializer json = Elevator.Core.Serialization.CoreJson.Create();

    static double M(double mm)
    {
        return mm / 1000;
    }

    static Body2 Primitive(string plane, double offset, double depth, Action<SketchManager> sketch)
    {
        int err = 0;
        construction = (ModelDoc2)sw.NewDocument(partTemplate, 0, 0, 0);
        construction.ClearSelection2(true);
        if (!construction.Extension.SelectByID2(plane, "PLANE", 0, 0, 0, false, 0, null, 0))
            throw new Exception("Plane missing " + plane);
        construction.SketchManager.InsertSketch(true);
        sketch(construction.SketchManager);
        construction.SketchManager.InsertSketch(true);
        var feature = construction.FeatureManager.FeatureExtrusion3(
            true,
            false,
            false,
            0,
            0,
            M(depth),
            0,
            false,
            false,
            false,
            false,
            0,
            0,
            false,
            false,
            false,
            false,
            false,
            false,
            true,
            offset == 0 ? 0 : 3,
            M(Math.Abs(offset)),
            offset < 0);
        if (feature == null)
        {
            throw new Exception(
                            "Primitive extrusion failed after "
                                + records.Count
                                + " solids, plane="
                                + plane
                                + " offset="
                                + offset
                                + " depth="
                                + depth);
        }
        var body = (Body2)((Face2)((object[])feature.GetFaces())[0]).GetBody();
        var copy = (Body2)body.Copy2(false);
        sw.CloseDoc(construction.GetTitle());
        construction = null;
        sw.ActivateDoc3(model.GetTitle(), false, 0, ref err);
        return copy;
    }

    static Body2 Box(double x, double y, double z, double w, double d, double h)
    {
        if (w <= 0 || d <= 0 || h <= 0)
            throw new Exception("Invalid box dimension");
        return Primitive("Front Plane", z, h, s => s.CreateCornerRectangle(M(x), M(y), 0, M(x + w), M(y + d), 0));
    }

    static Body2 Cylinder(double x, double y, double z, double diameter, double length)
    {
        return Primitive("Right Plane", x, length, s => s.CreateCircleByRadius(M(-z), M(y), 0, M(diameter / 2)));
    }

    static Body2 Boolean(Body2 a, Body2 b, int op)
    {
        int error;
        var result = (object[])a.Operations2(op, b, out error);
        if (result == null || result.Length != 1)
            throw new Exception("Boolean failed: " + error);
        return (Body2)result[0];
    }

    static double[] Bounds(Body2 b)
    {
        double[] r = new double[6];
        for (int axis = 0; axis < 3; axis++)
        {
            for (int sign = -1; sign
                        <= 1; sign += 2)
            {
                double x, y, z;
                double[] dir = new double[3];
                dir[axis] = sign;
                if (!b.GetExtremePoint(
            dir[0],
            dir[1],
            dir[2],
            out x,
            out y,
            out z))
                    throw new Exception("Extreme point failed");
                r[axis + (sign == 1 ? 3 : 0)] = 1000 * (axis == 0 ? x : axis == 1 ? y : z);
            }
        }
        return r;
    }

    static double Volume(Body2 b)
    {
        return ((double[])b.GetMassProperties(1))[3] * 1e9;
    }

    static BodyRecord Record(Body2 b, string name, string kind)
    {
        var faults = b.Check3;
        return new BodyRecord
        {
            Name = name,
            Kind = kind,
            BoundsMm = Bounds(b),
            VolumeMm3 = Volume(b),
            FaultCount = faults == null ? 0 : faults.Count
        };
    }

    static void Add(string name, Body2 body, string kind = "box")
    {
        if (body == null)
            throw new Exception("Null body " + name);
        var feature = (Feature)part.CreateFeatureFromBody3(body, false, 1);
        if (feature == null)
            throw new Exception("Insert failed " + name);
        feature.Name = name;
        var actual = (Body2)((Face2)((object[])feature.GetFaces())[0]).GetBody();
        actual.Name = name;
        // Check the inserted persistent body, rather than asking an out-of-process temporary Boolean body for mass properties.
        var r = Record(actual, name, kind);
        if (r.FaultCount != 0 || r.VolumeMm3 <= 0)
            throw new Exception("Invalid solid " + name);
        records.Add(r);
        solids.Add(actual);
        if (records.Count % 6 == 0)
            Console.WriteLine("Building model: " + records.Count + " named solids…");
    }

    static void B(string n, double x, double y, double z, double w, double d, double h)
    {
        Add(n, Box(x, y, z, w, d, h));
    }

    static void C(string n, double x, double y, double z, double diameter, double length)
    {
        Add(n, Cylinder(x, y, z, diameter, length), "cylinder-axis-X");
    }

    static void Rail(string n, double contact, double row, double flangeWidth, double depth, double web, bool left, double z0, double h)
    {
        double flange = left ? contact - depth : contact + depth - web;
        double wx = left ? flange + web : contact;
        var a = Box(flange, row - flangeWidth / 2, z0, web, flangeWidth, h);
        var b = Box(wx, row - web / 2, z0, depth - web, web, h);
        Add(n, Boolean(a, b, (int)swBodyOperationType_e.SWBODYADD), "T-profile");
    }

    static void Geometry()
    {
        double c = p.SW / 2, under = p.Travel + p.Overhead, roof = under + p.RoofSlab, top = roof + p.RoomClearHeight;
        ShaftWalls(c, under);
        PitSlabsAndCeiling(c, under, top);
        Landings(c);
        CarAndCounterweight(c);
        GuideRails(c, under);
        SupportsAndBeams(roof);
        MachineAndSheaves(c, roof);
        Governor(roof);
    }

    private static void ShaftWalls(double c, double under)
    {
        B("Shaft_Left_Wall", -p.WallSide, -p.WallFront, -p.Pit, p.WallSide, p.SD + p.WallFront + p.WallRear, under + p.Pit);
        B("Shaft_Right_Wall", p.SW, -p.WallFront, -p.Pit, p.WallSide, p.SD + p.WallFront + p.WallRear, under + p.Pit);
        B("Shaft_Rear_Wall", 0, p.SD, -p.Pit, p.SW, p.WallRear, under + p.Pit);
        B("Shaft_Front_Left_Pier", 0, -p.WallFront, -p.Pit, c - p.DoorWidth / 2, p.WallFront, under + p.Pit);
        B("Shaft_Front_Right_Pier", c + p.DoorWidth / 2, -p.WallFront, -p.Pit, c - p.DoorWidth / 2, p.WallFront, under + p.Pit);
        double previous = -p.Pit;
        foreach (double z in p.FloorElevations.Where((z, i) => p.ServedFloors == null || p.ServedFloors[i]))
        {
            if (z > previous)
                B("Shaft_Front_Spandrel_" + z, c - p.DoorWidth / 2, -p.WallFront, previous, p.DoorWidth, p.WallFront, z - previous);
            previous = z + p.DoorConstructionHeight;
        }
        if (under > previous)
            B("Shaft_Front_Top_Spandrel", c - p.DoorWidth / 2, -p.WallFront, previous, p.DoorWidth, p.WallFront, under - previous);
    }

    private static void PitSlabsAndCeiling(double c, double under, double top)
    {
        B(
                    "Pit_Base",
                    -p.WallSide,
                    -p.WallFront,
                    -p.Pit - p.PitBaseThickness,
                    p.SW + 2 * p.WallSide,
                    p.SD + p.WallFront + p.WallRear,
                    p.PitBaseThickness);
        // Roof/floor openings follow both B-B rope drops. Opening X breadth is an editable assumption.
        var slab = Box(-p.RoomLeft, -p.RoomFront, under, p.SW + p.RoomLeft + p.RoomRight, p.SD + p.RoomFront + p.WallRear, p.RoofSlab);
        foreach (double row in new[] { p.CarRow, p.CwRow })
        {
            slab = Boolean(
                            slab,
                            Box(c - p.PulleyWidth / 2, row - 80, under - 1, p.PulleyWidth, 160, p.RoofSlab + 2),
                            (int)swBodyOperationType_e.SWBODYCUT);
        }
        Add("MachineRoom_Floor_With_Rope_Openings", slab, "slab-with-two-openings");
        B(
            "MachineRoom_Ceiling",
            -p.RoomLeft,
            -p.RoomFront,
            top,
            p.SW + p.RoomLeft + p.RoomRight,
            p.SD + p.RoomFront + p.WallRear,
            p.CeilingThickness);
    }

    private static void Landings(double c)
    {
        for (int i = 0; i < p.FloorElevations.Length; i++)
        {
            double z = p.FloorElevations[i];
            B(
                "Landing_" + i + "_Slab",
                -p.WallSide,
                -p.WallFront - p.LandingApronDepth,
                z - p.LandingSlab,
                p.SW + 2 * p.WallSide,
                p.LandingApronDepth,
                p.LandingSlab);
            if (p.ServedFloors != null && !p.ServedFloors[i])
                continue;
            B(
                "Landing_" + i + "_Door_Left",
                c - p.DoorWidth / 2,
                -p.DoorThickness / 2,
                z,
                p.DoorWidth / 2,
                p.DoorThickness,
                p.DoorClearHeight);
            B("Landing_" + i + "_Door_Right", c, -p.DoorThickness / 2, z, p.DoorWidth / 2, p.DoorThickness, p.DoorClearHeight);
        }
    }

    private static void CarAndCounterweight(double c)
    {
        double left = c - p.CarWidth / 2, right =
                    c + p.CarWidth / 2, front = p.CarFront, rear = front + p.CarDepth, cz = p.CarFloorZ, t = p.PanelThickness;
        B("Cabin_Left_Panel", left - t, front, cz, t, p.CarDepth, p.CarInternalHeight);
        B("Cabin_Right_Panel", right, front, cz, t, p.CarDepth, p.CarInternalHeight);
        B("Cabin_Rear_Panel", left - t, rear, cz, p.CarWidth + 2 * t, t, p.CarInternalHeight);
        B("Cabin_Floor", left - t, front, cz - p.FloorThickness, p.CarWidth + 2 * t, p.CarDepth + t, p.FloorThickness);
        B("Cabin_Roof", left - t, front, cz + p.CarInternalHeight, p.CarWidth + 2 * t, p.CarDepth + t, t);
        double f = p.FrameMemberWidth, fd = p.FrameMemberDepth, fb = cz + p.CarFrameBottom, ft = cz + p.CarFrameTop;
        B("Car_Frame_Left_Upright", left - t - f, p.CarRow - fd / 2, fb + f, f, fd, ft - fb - 2 * f);
        B("Car_Frame_Right_Upright", right + t, p.CarRow - fd / 2, fb + f, f, fd, ft - fb - 2 * f);
        B("Car_Frame_Lower_Crosshead", left - t - f, p.CarRow - fd / 2, fb, p.CarWidth + 2 * (t + f), fd, f);
        B("Car_Frame_Upper_Crosshead", left - t - f, p.CarRow - fd / 2, ft - f, p.CarWidth + 2 * (t + f), fd, f);
        B("Cabin_Door_Left", c - p.DoorWidth / 2, front - p.DoorThickness, cz, p.DoorWidth / 2, p.DoorThickness, p.DoorClearHeight);
        B("Cabin_Door_Right", c, front - p.DoorThickness, cz, p.DoorWidth / 2, p.DoorThickness, p.DoorClearHeight);
        // Frame and infill partition the stated CW envelope without double-counted volume.
        double cwleft = c - p.CwWidth / 2, cwfront = p.CwRow - p.CwDepth / 2, cb = p.CwBottom;
        B("Counterweight_Frame_Left", cwleft, cwfront, cb, 25, p.CwDepth, p.CwHeight);
        B("Counterweight_Frame_Right", cwleft + p.CwWidth - 25, cwfront, cb, 25, p.CwDepth, p.CwHeight);
        B("Counterweight_Frame_Bottom", cwleft + 25, cwfront, cb, p.CwWidth - 50, p.CwDepth, 50);
        B("Counterweight_Frame_Top", cwleft + 25, cwfront, cb + p.CwHeight - 50, p.CwWidth - 50, p.CwDepth, 50);
        B("Counterweight_Infill", cwleft + 25, cwfront, cb + 50, p.CwWidth - 50, p.CwDepth, p.CwHeight - 100);
    }

    private static void GuideRails(double c, double under)
    {
        Rail("Car_Guide_Left_T70_65_9", c - p.CarDBG / 2, p.CarRow, 65, 70, 9, true, -p.Pit, under + p.Pit);
        Rail("Car_Guide_Right_T70_65_9", c + p.CarDBG / 2, p.CarRow, 65, 70, 9, false, -p.Pit, under + p.Pit);
        Rail("CW_Guide_Left_T50_50_5", c - p.CwDBG / 2, p.CwRow, 50, 50, 5, true, -p.Pit, under + p.Pit);
        Rail("CW_Guide_Right_T50_50_5", c + p.CwDBG / 2, p.CwRow, 50, 50, 5, false, -p.Pit, under + p.Pit);
    }

    private static void SupportsAndBeams(double roof)
    {
        B("Concrete_Front_Support", p.SupportLeftX, -200, roof, p.ConcreteWidth, p.ConcreteDepth, p.ConcreteHeight);
        B("Concrete_Rear_Support", p.SupportLeftX, p.SD, roof, p.ConcreteWidth, p.ConcreteDepth, p.ConcreteHeight);
        int bn = 0;
        foreach (double bx in new[] { p.Beam1X, p.Beam2X })
        {
            bn++;
            foreach (double by in new[] { p.FrontSupportCenterY, p.RearSupportCenterY })
                B("Beam_" + bn + "_Stand_" + by, bx, by - 40, roof + p.ConcreteHeight, p.BeamWidth, 80, p.SteelBottom - p.ConcreteHeight);
            double y = p.FrontSupportCenterY - 40, span = p.RearSupportCenterY - p.FrontSupportCenterY + 80, z = roof + p.SteelBottom;
            var ch = Box(bx, y, z, p.BeamWeb, span, p.SteelDepth);
            ch = Boolean(ch, Box(bx + p.BeamWeb, y, z, p.BeamWidth - p.BeamWeb, span, p.BeamFlange), (int)swBodyOperationType_e.SWBODYADD);
            ch = Boolean(
                ch,
                Box(bx + p.BeamWeb, y, z + p.SteelDepth - p.BeamFlange, p.BeamWidth - p.BeamWeb, span, p.BeamFlange),
                (int)swBodyOperationType_e.SWBODYADD);
            Add("Machine_Beam_" + bn + "_" + p.BeamDesignation + "_Simplified", ch, "channel");
        }
    }

    private static void MachineAndSheaves(double c, double roof)
    {
        B(
                    "Machine_Base",
                    p.MachineBaseX,
                    p.MachineBaseY,
                    roof + p.SteelBottom + p.SteelDepth,
                    317,
                    345,
                    p.MachineBaseTop - p.SteelBottom - p.SteelDepth);
        B("Machine_Gearbox_Envelope", p.MachineGearboxX, p.MachineBaseY, roof + p.MachineBaseTop, 180, 345, 470);
        B("Machine_Motor_Envelope", p.MachineMotorX, p.MachineMotorY, roof + p.MachineMotorZ, 132.1163139811, 300, 310.219740047408);
        C(
            "Traction_Sheave_PitchEnvelope",
            c - p.PulleyWidth / 2,
            p.TractionY,
            roof + p.TractionZAboveRoof,
            p.TractionDiameter,
            p.PulleyWidth);
        C(
            "Traction_Axle_Simplified",
            c + p.PulleyWidth / 2,
            p.TractionY,
            roof + p.TractionZAboveRoof,
            70,
            p.MachineGearboxX - c - p.PulleyWidth / 2);
        if (p.DeflectorVisible)
        {
            C(
                "Deflector_Sheave_PitchEnvelope",
                c - p.PulleyWidth / 2,
                p.DeflectorY,
                roof + p.DeflectorZAboveRoof,
                p.DeflectorDiameter,
                p.PulleyWidth);
            // Twin hanger side plates terminate at the axle center; kept outside the disk width.
            foreach (double hx in new[] { c - p.PulleyWidth / 2 - p.HangerWidth, c + p.PulleyWidth / 2 })
            {
                B(
                                    "Deflector_Hanger_" + hx,
                                    hx,
                                    p.DeflectorY - p.HangerDepth / 2,
                                    roof + p.DeflectorZAboveRoof + p.HangerBottomAboveDeflectorCenter,
                                    p.HangerWidth,
                                    p.HangerDepth,
                                    p.SteelBottom + p.SteelDepth - 10 - p.DeflectorZAboveRoof - p.HangerBottomAboveDeflectorCenter);
            }
        }
    }

    private static void Governor(double roof)
    {
        C(
                    "Governor_Sheave_Simplified",
                    p.GovernorX - p.GovernorWidth / 2,
                    p.GovernorY,
                    roof + p.GovernorZAboveRoof,
                    p.GovernorDiameter,
                    p.GovernorWidth);
        B("Governor_Stand_Simplified", p.GovernorX - 40, p.GovernorY - 80, roof, 80, 160, p.GovernorZAboveRoof - p.GovernorDiameter / 2);
    }

    static List<object> Intersections()
    {
        var hits = new List<object>();
        for (int i = 0; i < solids.Count; i++)
        {
            for (int j = i + 1; j < solids.Count; j++)
            {
                var a = records[i].BoundsMm;
                var b = records[j].BoundsMm;
                if (Enumerable.Range(0, 3).Any(k => Math.Min(a[k + 3], b[k + 3]) - Math.Max(a[k], b[k]) < 0.01))
                    continue;
                int error;
                object[] intersection = null;
                try
                {
                    intersection =
                        (object[])((Body2)solids[i].Copy2(false)).Operations2(
                            (int)swBodyOperationType_e.SWBODYINTERSECT,
                            solids[j].Copy2(false),
                            out error);
                }
                catch (Exception e)
                {
                    throw new Exception("Interference check failed: " + records[i].Name + " / " + records[j].Name, e);
                }
                double volume = intersection == null ? 0 : intersection.Cast<Body2>().Sum(x => Volume(x));
                if (volume > 0.01)
                    hits.Add(new { A = records[i].Name, B = records[j].Name, VolumeMm3 = volume });
            }
        }
        return hits;
    }

    /// <summary>
    /// STEP worker entry: builds the solids in SOLIDWORKS from the job folder, exports and re-verifies the STEP file; 0 on success.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        string folder = null;
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (args.Length != 1)
                throw new ArgumentException("Expected one export job folder.");
            folder = Path.GetFullPath(args[0]);
            p = json.Deserialize<StepModelParameters>(File.ReadAllText(Path.Combine(folder, "Parameters.json")));
            Console.WriteLine("Starting SOLIDWORKS solid model generation…");
            StartSolidWorks();
            Geometry();
            model.ForceRebuild3(false);
            Console.WriteLine("Checking solid geometry and component intersections…");
            var intersections = Intersections();
            int error = 0, warning = 0;
            string native = Path.Combine(folder, "Elevator_Assembly.SLDPRT"), step = Path.Combine(folder, "Elevator_Assembly.step");
            if (!model.Extension.SaveAs(native, 0, 1, null, ref error, ref warning) || error != 0)
                throw new Exception("Native save failed: " + error);
            File.WriteAllText(Path.Combine(folder, "source-bodies.json"), json.Serialize(records));
            Console.WriteLine("Exporting STEP AP214…");
            sw.ActivateDoc3(model.GetTitle(), false, 0, ref error);
            model = (ModelDoc2)sw.ActiveDoc;
            ExportStep(ref error, warning, step);
            Console.WriteLine("Reopening STEP to verify solids, units and dimensions…");
            sw.CloseDoc(model.GetTitle());
            model = null;
            model = (ModelDoc2)sw.LoadFile4(step, "", null, ref error);
            if (model == null || error != 0)
                throw new Exception("STEP reopen failed: " + error);
            var rb =
                ((object[])((PartDoc)model).GetBodies2(0, false)).Cast<Body2>().Select((b, i) => Record(b, b.Name, "STEP-import")).ToList();
            if (rb.Count != records.Count
                || rb.Any(b => b.FaultCount != 0)
                || Math.Abs(rb.Sum(b => b.VolumeMm3) - records.Sum(b => b.VolumeMm3)) > 1)
                throw new Exception("STEP solid/volume integrity mismatch");
            double max = CompareBodies(step, rb);
            var report = new
            {
                Tool = "SOLIDWORKS "
                + sw.RevisionNumber(),
                Units = "mm",
                STEP = "AP214",
                SourceSolidCount = records.Count,
                ReopenedSolidCount = rb.Count,
                FaultCount = rb.Sum(
                    b =>
                b.FaultCount),
                MaxReopenedBoundsDifferenceMm = max,
                SourceVolumeMm3 = records.Sum(
                    b => b.VolumeMm3),
                ReopenedVolumeMm3 = rb.Sum(b => b.VolumeMm3),
                Intersections = intersections,
                ReopenedBodies = rb
            };
            File.WriteAllText(Path.Combine(folder, "verification.json"), json.Serialize(report));
            Console.WriteLine("STEP verified: " + rb.Count + " solids; " + intersections.Count + " positive-volume intersections.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            if (folder != null)
                File.WriteAllText(Path.Combine(folder, "error.txt"), e.ToString());
            return 1;
        }
        finally
        {
            if (sw != null)
            {
                try
                {
                    if (construction != null)
                        sw.CloseDoc(construction.GetTitle());
                }
                catch { }
                try
                {
                    if (model
                != null)
                        sw.CloseDoc(model.GetTitle());
                }
                catch { }
                try
                {
                    if (!String.IsNullOrEmpty(previousActiveTitle))
                    {
                        int error =
                0;
                        sw.ActivateDoc3(previousActiveTitle, false, 0, ref error);
                    }
                    var remaining = (object[])sw.GetDocuments();
                    if (ownsApplication
                    && (remaining == null || remaining.Length == 0))
                        sw.ExitApp();
                }
                catch { }
            }
        }
    }

    private static void StartSolidWorks()
    {
        var prog = Type.GetTypeFromProgID("SldWorks.Application");
        if (prog == null)
            throw new Exception("STEP export requires an installed, licensed SOLIDWORKS with a configured part template.");
        sw = (SldWorks)Activator.CreateInstance(prog);
        var initialDocs = (object[])sw.GetDocuments();
        ownsApplication = initialDocs == null || initialDocs.Length == 0;
        var initialActive = (ModelDoc2)sw.ActiveDoc;
        if (initialActive != null)
            previousActiveTitle = initialActive.GetTitle();
        partTemplate = sw.GetDocumentTemplate(1, "", 0, 0, 0);
        if (String.IsNullOrEmpty(partTemplate) || !File.Exists(partTemplate))
            throw new Exception("Configure a default part template in SOLIDWORKS before STEP export.");
        sw.GetModeler();
        model = (ModelDoc2)sw.NewDocument(partTemplate, 0, 0, 0);
        if (model == null)
            throw new Exception("SOLIDWORKS could not create a part from its default template.");
        part = (PartDoc)model;
        model.SetUnits(0, 0, 0, 3, false);
    }

    private static void ExportStep(ref int error, int warning, string step)
    {
        int old = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP);
        try
        {
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, 214);
            model.ClearSelection2(true);
            if (!model.Extension.SaveAs(step, 0, 1, null, ref error, ref warning) || error != 0)
                throw new Exception("STEP export failed: " + error);
        }
        finally
        {
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, old);
        }
    }

    private static double CompareBodies(string step, List<BodyRecord> rb)
    {
        var remaining = new List<BodyRecord>(records);
        double max = 0;
        foreach (var b in rb)
        {
            var match = remaining.OrderBy(a => a.BoundsMm.Zip(b.BoundsMm, (x, y) => Math.Abs(x - y)).Max()).First();
            double delta = match.BoundsMm.Zip(b.BoundsMm, (x, y) => Math.Abs(x - y)).Max();
            max = Math.Max(max, delta);
            if (delta > 0.01 || Math.Abs(match.VolumeMm3 - b.VolumeMm3) > 1)
                throw new Exception("STEP per-body dimension mismatch");
            remaining.Remove(match);
        }
        string unitText = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(step), @"\s+", "");
        if (!unitText.Contains("SI_UNIT(.MILLI.,.METRE.)"))
            throw new Exception("STEP millimetre unit declaration is missing.");
        return max;
    }
}

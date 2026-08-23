using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using MyFirstProject.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Civil = Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.Profiles))]

namespace Civil3DCsharp
{
    public class Profiles
    {
        [CommandMethod("CTP_VeTracDoc_TuNhien")]
        public static void ProfileAndProfileViewCreate()
        {
            // get the surface
            _ = new            // get the surface
            UserInput();
            ObjectId surfaceId = UserInput.GSurfaceId("\n Chọn mặt phẳng " + "để vẽ trắc dọc tự nhiên:\n");

            // draw 100 profiles
            String i = "Enter";
            while (i == "Enter")
            {


                // start transantion
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // get alignment for the profiles
                        ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "để vẽ trắc dọc: \n");

                        // get point of the first profiles
                        Point3d basePoint = UserInput.GPoint("\n Chọn vị trí điểm" + " đặt trắc dọc:\n");


                        // get layer ID, surface ID, profiles style, profiles label, profile view style for the profiles 
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        ObjectId layerID = alignment.LayerId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        ObjectId profileStyleId = A.Cdoc.Styles.ProfileStyles["0.TN"];
                        ObjectId profileLabelSetId = A.Cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles["ĐTN (đường TN)"];
                        ObjectId profileViewStyleId = A.Cdoc.Styles.ProfileViewStyles["TDTN GT 1-1000"];
                        ObjectId profileBandStyleId = A.Cdoc.Styles.ProfileViewBandSetStyles["TRẮC DỌC DO THI"];

                        // create the profiles from surface and profile view
                        CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        string profileName = surface.Name + "-" + alignment.Name;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        ObjectId profilesId = Profile.CreateFromSurface(profileName, alignmentId, surfaceId, layerID, profileStyleId, profileLabelSetId);
                        ObjectId profileViewId = ProfileView.Create(alignmentId, basePoint, alignment.Name, profileBandStyleId, profileViewStyleId);
                        tr.Commit();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception e)
                    {
                        A.Ed.WriteMessage(e.Message);

                    }
                }
                i = UserInput.GStopWithESC();
            }
        }

        [CommandMethod("CTP_VeTracDoc_TuNhien_TatCaTuyen")]
        public static void CTPVeTracDocTuNhienTatCaTuyen()
        {
            // Show form first
            var form = new MyFirstProject.Civil_Tool.VeTracDocTatCaTuyenForm();
            var result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

            if (result != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // Get values from form
            ObjectId surfaceId = form.SelectedSurfaceId;
            int khoangCach = form.KhoangCach;
            string profileStyleName = form.ProfileStyleName;
            string profileLabelSetName = form.ProfileLabelSetName;
            string profileViewStyleName = form.ProfileViewStyleName;
            string profileViewBandSetName = form.ProfileViewBandSetName;
            List<ObjectId> selectedAlignmentIds = form.SelectedAlignmentIds;

            if (selectedAlignmentIds.Count == 0)
            {
                A.Ed.WriteMessage("\n Không có tim đường nào được chọn.");
                return;
            }

            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                // Get point for placing profiles
                Point3d basePoint = UserInput.GPoint("\n Chọn vị trí điểm" + " đặt trắc dọc:\n");
                
                // Sort selected alignments by name
                var sortedAlignments = new List<(ObjectId Id, string Name)>();
                foreach (ObjectId alignmentId in selectedAlignmentIds)
                {
                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                    if (alignment != null)
                    {
                        sortedAlignments.Add((alignmentId, alignment.Name));
                    }
                }
                sortedAlignments = sortedAlignments.OrderBy(a => a.Name).ToList();

                // Get style IDs with fallback
                ObjectId profileStyleId = GetStyleId(() => A.Cdoc.Styles.ProfileStyles[profileStyleName], 
                    () => A.Cdoc.Styles.ProfileStyles["0.TN"]);
                ObjectId profileLabelSetId = GetStyleId(() => A.Cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles[profileLabelSetName],
                    () => A.Cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"]);
                ObjectId profileViewStyleId = GetStyleId(() => A.Cdoc.Styles.ProfileViewStyles[profileViewStyleName],
                    () => A.Cdoc.Styles.ProfileViewStyles["TDTN GT 1-1000"]);
                ObjectId profileBandStyleId = GetStyleId(() => A.Cdoc.Styles.ProfileViewBandSetStyles[profileViewBandSetName],
                    () => A.Cdoc.Styles.ProfileViewBandSetStyles["TRẮC DỌC DO THI"]);

                // Draw all profiles (sorted by name)
                int x = 0;
                int skipped = 0;
                foreach (var alignmentInfo in sortedAlignments)
                {
                    ObjectId alignmentId = alignmentInfo.Id;
                    Alignment? alignment1 = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    x++;
                    Point3d basePointNext = new(basePoint.X, basePoint.Y - x * khoangCach, basePoint.Z);

                    // get layer ID
                    Autodesk.Civil.DatabaseServices.Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId layerID = alignment.LayerId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    // create the profiles from surface and profile view
                    CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    string profileName = surface.Name + "-" + alignment.Name;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    // Check if profile already exists - skip creating if it does
                    bool profileExists = false;
                    ObjectIdCollection existingProfileIds = alignment.GetProfileIds();
                    foreach (ObjectId existingProfileId in existingProfileIds)
                    {
                        Profile? existingProfile = tr.GetObject(existingProfileId, OpenMode.ForRead) as Profile;
                        if (existingProfile != null && existingProfile.Name == profileName)
                        {
                            profileExists = true;
                            A.Ed.WriteMessage($"\n Tuyến '{alignment.Name}' - Profile đã tồn tại, chỉ tạo ProfileView.");
                            skipped++;
                            break;
                        }
                    }

                    // Only create profile if it doesn't exist
                    if (!profileExists)
                    {
                        ObjectId profilesId = Profile.CreateFromSurface(profileName, alignmentId, surfaceId, layerID, profileStyleId, profileLabelSetId);
                    }

                    // Generate unique ProfileView name
                    string profileViewName = alignment.Name;
                    ObjectIdCollection existingProfileViewIds = alignment.GetProfileViewIds();
                    var existingNames = new HashSet<string>();
                    foreach (ObjectId pvId in existingProfileViewIds)
                    {
                        ProfileView? existingPV = tr.GetObject(pvId, OpenMode.ForRead) as ProfileView;
                        if (existingPV != null)
                        {
                            existingNames.Add(existingPV.Name);
                        }
                    }
                    
                    // If name already exists, append suffix number
                    if (existingNames.Contains(profileViewName))
                    {
                        int suffix = 1;
                        while (existingNames.Contains($"{alignment.Name}_{suffix}"))
                        {
                            suffix++;
                        }
                        profileViewName = $"{alignment.Name}_{suffix}";
                        A.Ed.WriteMessage($"\n Tuyến '{alignment.Name}' - ProfileView đã tồn tại, tạo mới với tên: {profileViewName}");
                    }

                    // Always create ProfileView with unique name
                    ObjectId profileViewId = ProfileView.Create(alignmentId, basePointNext, profileViewName, profileBandStyleId, profileViewStyleId);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }

                A.Ed.WriteMessage($"\n Đã tạo trắc dọc cho {x - skipped} tuyến centerline (đã chọn: {selectedAlignmentIds.Count}). Bỏ qua {skipped} tuyến đã có profile.");
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        /// <summary>
        /// Helper method to get style ID with fallback
        /// </summary>
        private static ObjectId GetStyleId(Func<ObjectId> primary, Func<ObjectId> fallback)
        {
            try
            {
                return primary();
            }
            catch
            {
                try
                {
                    return fallback();
                }
                catch
                {
                    return ObjectId.Null;
                }
            }
        }

        [CommandMethod("CTP_Fix_DuongTuNhien_TheoCoc")]
        public static void CTPFixDuongTuNhienTheoCoc()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId profileId = UserInput.GProfileId("\n Chọn đường tự nhiên " + "để sửa theo cọc: ");
                Profile? profile = tr.GetObject(profileId, OpenMode.ForWrite) as Profile;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                profile.StyleName = "Existing Ground Profile (KHONG IN)";
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                ObjectId alignmentId = profile.AlignmentId;
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;

                // get stations of samplelines
#pragma warning disable CS8604 // Possible null reference argument.
                UtilitiesC3D.GCoordinatePointFromAlignment(alignment, 0, out string[] samplelineName, out string[] eastings, out string[] northings, out string[] stations);
#pragma warning restore CS8604 // Possible null reference argument.

                // Get profile start and end stations for validation
                double profileStartStation = profile.StartingStation;
                double profileEndStation = profile.EndingStation;
                A.Ed.WriteMessage($"\n Profile range: {profileStartStation:F3} to {profileEndStation:F3}");

                // Filter and sort unique stations within profile range
                var validStations = new List<double>();
                var uniqueStations = new HashSet<double>();

                foreach (string stationStr in stations)
                {
                    if (double.TryParse(stationStr, out double stationValue))
                    {
                        // Only include stations within profile range and avoid duplicates
                        if (stationValue >= profileStartStation && stationValue <= profileEndStation)
                        {
                            // Round to avoid floating point precision issues
                            double roundedStation = Math.Round(stationValue, 2);
                            if (uniqueStations.Add(roundedStation))
                            {
                                validStations.Add(roundedStation);
                            }
                        }
                    }
                }

                // Sort stations
                validStations.Sort();
                A.Ed.WriteMessage($"\n Số station hợp lệ: {validStations.Count}");

                // Display valid stations
                A.Ed.WriteMessage("\n Lý trình các cọc hợp lệ: ");
                foreach (double station in validStations)
                {
                    A.Ed.WriteMessage(station.ToString("F2") + ", ");
                }

                //get elevation for valid stations
                var stationElevations = new List<(double station, double elevation)>();
                
                for (int i = 0; i < validStations.Count; i++)
                {
                    try
                    {
                        double elevation = profile.ElevationAt(validStations[i]);
                        stationElevations.Add((validStations[i], elevation));
                        A.Ed.WriteMessage($"\n Lý trình: {validStations[i]:F2} Cao độ: {elevation:F3}");
                    }
                    catch (System.ArgumentException)
                    {
                        A.Ed.WriteMessage($"\n Lỗi lấy cao độ tại station {validStations[i]:F2}: Value does not fall within the expected range.");
                        // Skip this station instead of using interpolated value
                        continue;
                    }
                }

                if (stationElevations.Count == 0)
                {
                    A.Ed.WriteMessage("\n Không có station hợp lệ nào để tạo profile.");
                    return;
                }

                //create a fix profile
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId layerID = alignment.LayerId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                ObjectId profileStyleId = A.Cdoc.Styles.ProfileStyles["0.TN"];
                ObjectId profileLabelSetId = A.Cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];
                ObjectId fixProfileId = Profile.CreateByLayout("Fix-" + profile.Name, alignmentId, layerID, profileStyleId, profileLabelSetId);
                Profile? fixProfile = tr.GetObject(fixProfileId, OpenMode.ForWrite) as Profile;

                // Add PVIs with additional validation
                int successCount = 0;
                foreach (var (station, elevation) in stationElevations)
                {
                    try
                    {
                        // Additional check: ensure station is within alignment range
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (station >= alignment.StartingStation && station <= alignment.EndingStation)
                        {
                            fixProfile.PVIs.AddPVI(station, elevation);
                            successCount++;
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n Station {station:F2} nằm ngoài phạm vi alignment.");
                        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }
                    catch (System.ArgumentException ex)
                    {
                        A.Ed.WriteMessage($"\n Lỗi thêm PVI tại station {station:F2}: {ex.Message}");
                    }
                }

                A.Ed.WriteMessage($"\n Đã tạo thành công {successCount} PVI trong tổng số {stationElevations.Count} station.");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTP_GanNhanNutGiao_LenTracDoc")]
        public static void CTPGanNhanNutGiaoLenTracDoc()
        {
            // Show form first
            var form = new MyFirstProject.Civil_Tool.GanNhanNutGiaoForm();
            var result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

            if (result != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // Get values from form
            bool isCustom = form.IsCustomSelection;
            ObjectId pointGroupId = form.SelectedPointGroupId;
            bool limitOffset = form.LimitOffset;
            double saiSo = form.SaiSo;
            bool filterByStationRange = form.FilterByStationRange;
            List<ObjectId> profileViewIds = form.SelectedProfileViewIds;

            if (profileViewIds.Count == 0)
            {
                A.Ed.WriteMessage("\n Không có ProfileView nào được chọn.");
                return;
            }

            // Get points based on user source selection
            ObjectIdCollection pointIds = new ObjectIdCollection();
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    if (isCustom)
                    {
                        foreach (ObjectId id in form.CustomSelectedPointIds)
                        {
                            if (id.IsValid && !id.IsNull) pointIds.Add(id);
                        }
                    }
                    else if (pointGroupId == ObjectId.Null)
                    {
                        pointIds = UtilitiesC3D.GetAllCogoPointIds();
                    }
                    else
                    {
                        pointIds = UtilitiesC3D.GPointIdsFromPointGroup(pointGroupId);
                    }

                    if (pointIds.Count == 0)
                    {
                        A.Ed.WriteMessage("\n Cảnh báo: Không tìm thấy điểm CogoPoint nào từ nguồn đã chọn.");
                        return;
                    }
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n Lỗi khi lấy danh sách điểm: {ex.Message}");
                    return;
                }
            }

            A.Ed.WriteMessage($"\n=== BẮT ĐẦU XỬ LÝ {profileViewIds.Count} PROFILEVIEW (Tổng {pointIds.Count} điểm CogoPoint) ===");
            int processedCount = 0;

            // Process each selected ProfileView
            foreach (ObjectId profileViewId in profileViewIds)
            {
                try
                {
                    using (Transaction innerTr = A.Db.TransactionManager.StartTransaction())
                    {
                        ProfileView? profileView = innerTr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;
                        if (profileView == null)
                        {
                            A.Ed.WriteMessage("\n Lỗi: Profile view không hợp lệ.");
                            innerTr.Commit();
                            continue;
                        }

                        // Get alignment from ProfileView
                        ObjectId alignmentId = profileView.AlignmentId;
                        Alignment? alignment = innerTr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        
                        if (alignment == null)
                        {
                            A.Ed.WriteMessage($"\n Lỗi: Không thể lấy alignment từ ProfileView '{profileView.Name}'.");
                            innerTr.Commit();
                            continue;
                        }

                        double pvStationStart = profileView.StationStart;
                        double pvStationEnd = profileView.StationEnd;

                        A.Ed.WriteMessage($"\n\n--------------------------------------------------");
                        A.Ed.WriteMessage($"\n Đang xử lý ProfileView: {profileView.Name}");
                        A.Ed.WriteMessage($"\n Alignment: {alignment.Name} (Lý trình ProfileView: {pvStationStart:F2}m -> {pvStationEnd:F2}m)");
                        
                        // Filter points that belong to this ProfileView / alignment
                        ObjectIdCollection validPointIds = new ObjectIdCollection();
                        int totalPoints = 0;
                        int inStationPoints = 0;
                        int validPoints = 0;
                        
                        foreach (ObjectId pointId in pointIds)
                        {
                            totalPoints++;
                            CogoPoint? point = null;
                            try
                            {
                                point = innerTr.GetObject(pointId, OpenMode.ForRead) as CogoPoint;
                            }
                            catch { }

                            if (point == null) continue;

                            double station = 0;
                            double offset = 0;
                            
                            try
                            {
                                alignment.StationOffset(point.Easting, point.Northing, ref station, ref offset);
                                
                                // Check station range if requested
                                bool passStation = true;
                                if (filterByStationRange)
                                {
                                    passStation = (station >= pvStationStart - 0.1 && station <= pvStationEnd + 0.1);
                                }

                                if (passStation)
                                {
                                    inStationPoints++;
                                    
                                    // Check offset limit if requested
                                    bool passOffset = true;
                                    if (limitOffset)
                                    {
                                        passOffset = (Math.Abs(offset) <= saiSo);
                                    }

                                    if (passOffset)
                                    {
                                        validPointIds.Add(pointId);
                                        validPoints++;
                                    }
                                }
                            }
                            catch (System.Exception)
                            {
                                // Skip points that cannot be projected to this alignment
                            }
                        }
                        
                        A.Ed.WriteMessage($"\n Điểm trong phạm vi lý trình: {inStationPoints}/{totalPoints}");
                        if (limitOffset)
                        {
                            A.Ed.WriteMessage($"\n Điểm thỏa mãn sai số offset (≤ {saiSo}m): {validPoints}/{inStationPoints}");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n (Không giới hạn offset - lấy toàn bộ {validPoints} điểm trong phạm vi lý trình)");
                        }
                        
                        if (validPointIds.Count == 0)
                        {
                            A.Ed.WriteMessage($"\n CẢNH BÁO: Không có điểm nào thỏa mãn điều kiện để gắn lên ProfileView '{profileView.Name}'.");
                            innerTr.Commit();
                            continue;
                        }
                        
                        // Set selection and execute command
                        try
                        {
                            ObjectId[] pointArray = UserInput.ConvertObjectIdCollectionToArray(validPointIds);
                            A.Ed.SetImpliedSelection(pointArray);
                            
                            // Commit inner transaction before using Editor.Command
                            innerTr.Commit();
                            
                            // Execute the AutoCAD command
                            A.Ed.Command("_AECCPROJECTOBJECTSTOPROF", profileViewId);
                            
                            A.Ed.WriteMessage($"\n ✓ ĐÃ GẮN THÀNH CÔNG {validPointIds.Count} điểm lên ProfileView '{profileView.Name}'");
                            processedCount++;
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n Lỗi khi thực hiện lệnh project objects to profile: {ex.Message}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n Lỗi khi xử lý ProfileView: {ex.Message}");
                }
            }
            
            A.Ed.WriteMessage($"\n\n=== HOÀN THÀNH GẮN NHÃN ===");
            A.Ed.WriteMessage($"\n Đã xử lý thành công: {processedCount}/{profileViewIds.Count} ProfileView(s)");
        }

        /* lệnh tạo cogo point từ điểm PVI trên trắc dọc
         */
        [CommandMethod("CTP_TaoCogoPointTuPVI")]
        public static void CTPTaoCogoPointTuPVI()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                //get profile
                ObjectId profileId = UserInput.GProfileId("\n Chọn trắc dọc " + "để tạo điểm cogo point từ PVI: \n");
                Profile? profile = tr.GetObject(profileId, OpenMode.ForWrite) as Profile;

                //get alignment
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId alignmentId = profile.AlignmentId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;

                ProfilePVICollection pviIds = profile.PVIs;
                foreach (ProfilePVI pviId in pviIds)
                {
                    //lấy lý trình của điểm PVI
                    double station = pviId.RawStation;
                    double elevation = pviId.Elevation;

                    // set eathings, northings
                    double easting = 0;
                    double northing = 0;

                    //lấy tọa độ X, Y của điểm đựa vào lý trình trên tuyến
                    // ddihn
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    alignment.PointLocation(station, 0, ref easting, ref northing);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    //tạo điểm cogo point
                    UtilitiesC3D.CreateCogoPointFromPoint3D(new Point3d(easting, northing, elevation), profile.Name);

                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
    }
}


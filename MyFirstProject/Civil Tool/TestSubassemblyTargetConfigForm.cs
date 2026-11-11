using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;
using System.Windows.Forms;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.TestSubassemblyTargetConfigForm))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Test command for SubassemblyTargetConfigForm
    /// Allows testing the form with real corridor data
    /// </summary>
    public class TestSubassemblyTargetConfigForm
    {
        [CommandMethod("CTC_Cau_Hinh_Target_Corridor")]
        public static void TestTargetConfigForm()
        {
            A.Ed.WriteMessage("\n=== TEST SUBASSEMBLY TARGET CONFIG FORM ===");
            
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Step 1: Select a corridor
                    A.Ed.WriteMessage("\n\n--- Bước 1: Chọn Corridor ---");
                    ObjectId corridorId = UserInput.GCorridorId("\nChọn corridor để test: ");
                    
                    if (corridorId == ObjectId.Null)
                    {
                        A.Ed.WriteMessage("\n❌ Không có corridor được chọn.");
                        tr.Abort();
                        return;
                    }

                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForRead) as Corridor;
                    if (corridor == null || corridor.Baselines.Count == 0)
                    {
                        A.Ed.WriteMessage("\n❌ Corridor không hợp lệ hoặc không có baseline.");
                        tr.Abort();
                        return;
                    }

                    A.Ed.WriteMessage($"\n✅ Đã chọn corridor: {corridor.Name}");
                    A.Ed.WriteMessage($"\n   Số baselines: {corridor.Baselines.Count}");

                    // Step 2: Select a baseline (or use first one)
                    Baseline baseline = corridor.Baselines[0];
                    A.Ed.WriteMessage($"\n✅ Sử dụng baseline: {baseline.Name}");
                    
                    if (baseline.BaselineRegions.Count == 0)
                    {
                        A.Ed.WriteMessage("\n❌ Baseline không có regions.");
                        tr.Abort();
                        return;
                    }

                    // Step 3: Select a baseline region (or use first one)
                    BaselineRegion baselineRegion = baseline.BaselineRegions[0];
                    A.Ed.WriteMessage($"\n✅ Sử dụng baseline region: {baselineRegion.Name}");

                    // Step 4: Get targets from baseline region
                    SubassemblyTargetInfoCollection targetInfoCollection = baselineRegion.GetTargets();
                    A.Ed.WriteMessage($"\n✅ Số lượng subassembly targets: {targetInfoCollection.Count}");

                    if (targetInfoCollection.Count == 0)
                    {
                        A.Ed.WriteMessage("\n❌ Baseline region không có subassembly targets.");
                        tr.Abort();
                        return;
                    }

                    // Step 5: Prepare target collections
                    A.Ed.WriteMessage("\n\n--- Bước 2: Chuẩn bị Target Collections ---");

                    // Get alignments
                    ObjectIdCollection alignmentTargets = new ObjectIdCollection();
                    var alignmentIds = A.Cdoc.GetAlignmentIds();
                    foreach (ObjectId alignId in alignmentIds)
                    {
                        alignmentTargets.Add(alignId);
                    }
                    A.Ed.WriteMessage($"\n✅ Alignments: {alignmentTargets.Count}");

                    // Get profiles
                    ObjectIdCollection profileTargets = new ObjectIdCollection();
                    foreach (ObjectId alignId in alignmentIds)
                    {
                        Alignment? align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                        if (align != null)
                        {
                            foreach (ObjectId profileId in align.GetProfileIds())
                            {
                                profileTargets.Add(profileId);
                            }
                        }
                    }
                    A.Ed.WriteMessage($"\n✅ Profiles: {profileTargets.Count}");

                    // Get surfaces
                    ObjectIdCollection surfaceTargets = new ObjectIdCollection();
                    var surfaceIds = A.Cdoc.GetSurfaceIds();
                    foreach (ObjectId surfId in surfaceIds)
                    {
                        surfaceTargets.Add(surfId);
                    }
                    A.Ed.WriteMessage($"\n✅ Surfaces: {surfaceTargets.Count}");

                    // Polylines - would need to be selected manually, so we'll use empty collection for test
                    ObjectIdCollection polylineTargets = new ObjectIdCollection();
                    A.Ed.WriteMessage($"\n✅ Polylines: {polylineTargets.Count} (empty for test)");

                    // Step 6: Show the form
                    A.Ed.WriteMessage("\n\n--- Bước 3: Hiển thị Form ---");
                    
                    var targetConfigForm = new SubassemblyTargetConfigForm(
                        targetInfoCollection,
                        alignmentTargets,
                        profileTargets,
                        surfaceTargets,
                        polylineTargets);

                    var dialogResult = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(targetConfigForm);

                    if (dialogResult == DialogResult.OK && targetConfigForm.ConfigurationApplied)
                    {
                        A.Ed.WriteMessage("\n\n=== Kết quả cấu hình ===");
                        A.Ed.WriteMessage($"\n✅ Người dùng đã áp dụng cấu hình.");
                        A.Ed.WriteMessage($"\n✅ Số lượng target connections: {targetConfigForm.TargetConnections.Count}");
                        
                        // Display configuration details
                        foreach (var connection in targetConfigForm.TargetConnections)
                        {
                            string groupName = GetGroupName(connection.TargetGroupId);
                            A.Ed.WriteMessage($"\n   • Subassembly {connection.SubassemblyIndex + 1}: {groupName} - {connection.TargetOption}");
                        }

                        // Ask if user wants to apply to baseline region
                        A.Ed.WriteMessage("\n\nBạn có muốn áp dụng cấu hình này vào baseline region? (Y/N): ");
                        string? applyChoice = UserInput.GString("\nÁp dụng cấu hình");
                        
                        if (applyChoice?.ToUpper() == "Y")
                        {
                            A.Ed.WriteMessage("\n\n--- Áp dụng cấu hình vào Baseline Region ---");
                            
                            // No need to upgrade open - already in transaction
#pragma warning disable CS0618 // Type or member is obsolete
                            targetConfigForm.ApplyTargetConnectionsToBaselineRegion(baselineRegion);
#pragma warning restore CS0618 // Type or member is obsolete
                            
                            tr.Commit();
                            A.Ed.WriteMessage("\n✅ Đã áp dụng cấu hình thành công!");
                        }
                        else
                        {
                            A.Ed.WriteMessage("\n⚠️ Không áp dụng cấu hình. Transaction sẽ bị hủy.");
                            tr.Abort();
                        }
                    }
                    else
                    {
                        A.Ed.WriteMessage("\n⚠️ Người dùng đã hủy form.");
                        tr.Abort();
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                    A.Ed.WriteMessage($"\n   Stack trace: {ex.StackTrace}");
                    tr.Abort();
                }
            }
        }

        [CommandMethod("CTC_Thong_Tin_Target_Corridor")]
        public static void TestTargetConfigFormDebug()
        {
            A.Ed.WriteMessage("\n=== TEST SUBASSEMBLY TARGET CONFIG FORM (DEBUG MODE) ===");
            
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Select corridor
                    ObjectId corridorId = UserInput.GCorridorId("\nChọn corridor: ");
                    
                    if (corridorId == ObjectId.Null)
                    {
                        A.Ed.WriteMessage("\n❌ Không có corridor được chọn.");
                        tr.Abort();
                        return;
                    }

                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForRead) as Corridor;
                    if (corridor == null)
                    {
                        A.Ed.WriteMessage("\n❌ Corridor không hợp lệ.");
                        tr.Abort();
                        return;
                    }

                    A.Ed.WriteMessage($"\n\n=== CORRIDOR DEBUG INFO ===");
                    A.Ed.WriteMessage($"\nCorridor: {corridor.Name}");
                    A.Ed.WriteMessage($"Baselines: {corridor.Baselines.Count}");

                    // Show all baselines and regions
                    for (int i = 0; i < corridor.Baselines.Count; i++)
                    {
                        Baseline baseline = corridor.Baselines[i];
                        A.Ed.WriteMessage($"\n\n--- Baseline {i + 1}: {baseline.Name} ---");
                        A.Ed.WriteMessage($"Regions: {baseline.BaselineRegions.Count}");

                        for (int j = 0; j < baseline.BaselineRegions.Count; j++)
                        {
                            BaselineRegion region = baseline.BaselineRegions[j];
                            A.Ed.WriteMessage($"\n  Region {j + 1}: {region.Name}");
                            
                            SubassemblyTargetInfoCollection targets = region.GetTargets();
                            A.Ed.WriteMessage($"  Subassembly targets: {targets.Count}");

                            // Show target details
                            for (int k = 0; k < targets.Count; k++)
                            {
                                var targetInfo = targets[k];
                                
                                A.Ed.WriteMessage($"\n    Target {k + 1}:");
                                
                                try
                                {
                                    string subassemblyName = targetInfo.SubassemblyName;
                                    A.Ed.WriteMessage($"      SubassemblyName: {subassemblyName}");
                                }
                                catch (System.Exception ex)
                                {
                                    A.Ed.WriteMessage($"      SubassemblyName: [Error: {ex.Message}]");
                                }

                                try
                                {
                                    string targetType = targetInfo.TargetType.ToString();
                                    A.Ed.WriteMessage($"      TargetType: {targetType}");
                                }
                                catch (System.Exception ex)
                                {
                                    A.Ed.WriteMessage($"      TargetType: [Error: {ex.Message}]");
                                }

                                A.Ed.WriteMessage($"      Current TargetIds count: {targetInfo.TargetIds?.Count ?? 0}");
                            }
                        }
                    }

                    tr.Commit();
                    A.Ed.WriteMessage("\n\n=== DEBUG INFO END ===");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                    tr.Abort();
                }
            }
        }

        private static string GetGroupName(int groupId)
        {
            return groupId switch
            {
                0 => "Alignments",
                1 => "Profiles",
                2 => "Surfaces",
                3 => "Polylines",
                _ => "Không gắn kết"
            };
        }
    }
}

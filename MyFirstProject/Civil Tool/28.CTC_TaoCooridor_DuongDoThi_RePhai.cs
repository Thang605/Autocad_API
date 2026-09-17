using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using MyFirstProject;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices.Styles;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using System.Linq;
using System.Windows.Forms;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTC_TaoCooridor_DuongDoThi_RePhai_Commands))]

namespace Civil3DCsharp
{
    class CTC_TaoCooridor_DuongDoThi_RePhai_Commands
    {
        private const string SUCCESS_INDICATOR = "[OK]";
        private const string ERROR_INDICATOR = "[X]";
        private const string WARNING_INDICATOR = "[!]";

        // Static fields to save configuration for next run
        private static TargetMappingConfiguration? _savedTargetMapping = null;
        private static string? _savedSurfaceName = null;

        [CommandMethod("CAC_TaoCooridor_DuongDoThi_RePhai")]
        public static void CAC_TaoCooridor_DuongDoThi_RePhai()
        {
            if (A.Doc == null) return;

            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                var executionResult = ExecuteCorridorCreation(tr);

                if (executionResult.Success)
                {
                    tr.Commit();
                    A.Ed.WriteMessage($"\n{SUCCESS_INDICATOR} Hoàn thành: {executionResult.Message}");
                }
                else
                {
                    tr.Abort();
                    A.Ed.WriteMessage($"\n{ERROR_INDICATOR} Lỗi: {executionResult.Message}");
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n{ERROR_INDICATOR} Lỗi AutoCAD: {e.Message}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n{ERROR_INDICATOR} Lỗi hệ thống: {ex.Message}");
                tr.Abort();
            }
        }

        private static ExecutionResult ExecuteCorridorCreation(Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage("\n================================================");
                A.Ed.WriteMessage("\n  TAO CORRIDOR RE PHAI - DUONG DO THI");
                A.Ed.WriteMessage("\n================================================");

                // Step 1: Get user input from FORM (replaced command line)
                var inputResult = GetUserInputFromForm(tr);
                if (!inputResult.Success)
                {
                    return new ExecutionResult { Success = false, Message = inputResult.Message };
                }

                var formData = inputResult.Data;
                A.Ed.WriteMessage($"\nBắt đầu tạo {formData.AlignmentNumber} corridor rẽ phải...");

                // Step 2: Validate and get required objects
                var objectsResult = ValidateAndGetObjects(tr, formData);
                if (!objectsResult.Success)
                {
                    return new ExecutionResult { Success = false, Message = objectsResult.Message };
                }

                var objects = objectsResult.Data;
                LogObjectInfo(objects);

                // Step 3: Process alignment-polyline pairs
                return ProcessAlignmentPolylinePairs(tr, formData, objects);
            }
            catch (System.Exception ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Message = $"Lỗi không mong đợi: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get user input from Windows Form
        /// </summary>
        private static ExecutionResult<CorridorFormData> GetUserInputFromForm(Transaction tr)
        {
            try
            {
                using (var form = new CorridorRePhai_InputForm())
                {
                    var dialogResult = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

                    if (dialogResult != DialogResult.OK || !form.FormAccepted)
                    {
                        return new ExecutionResult<CorridorFormData>
                        {
                            Success = false,
                            Message = "Người dùng đã hủy thao tác."
                        };
                    }

                    // Get basic data from form
                    ObjectId corridorId = form.SelectedCorridorId;
                    ObjectId targetId1 = form.TargetAlignment1Id;
                    ObjectId targetId2 = form.TargetAlignment2Id;
                    ObjectId assemblyId = form.SelectedAssemblyId;
                    string assemblyName = form.SelectedAssemblyName;
                    int alignmentNumber = form.NumberOfTurnAlignments;

                    // Validate objects from form
                    var corridor = tr.GetObject(corridorId, OpenMode.ForRead) as Corridor;
                    if (corridor == null)
                    {
                        return new ExecutionResult<CorridorFormData>
                        {
                            Success = false,
                            Message = "Không thể lấy đối tượng corridor."
                        };
                    }

                    var alignment1 = tr.GetObject(targetId1, OpenMode.ForRead) as Alignment;
                    var alignment2 = tr.GetObject(targetId2, OpenMode.ForRead) as Alignment;

                    if (alignment1 == null || alignment2 == null)
                    {
                        return new ExecutionResult<CorridorFormData>
                        {
                            Success = false,
                            Message = "Không thể lấy đối tượng target alignment."
                        };
                    }

                    // Log form selections
                    A.Ed.WriteMessage($"\n✓ Corridor: {corridor.Name}");
                    A.Ed.WriteMessage($"\n✓ Target Alignment 1: {alignment1.Name}");
                    A.Ed.WriteMessage($"\n✓ Target Alignment 2: {alignment2.Name}");
                    A.Ed.WriteMessage($"\n✓ Assembly: {assemblyName}");
                    A.Ed.WriteMessage($"\n✓ Số lượng đường rẽ phải: {alignmentNumber}");

                    // Now select alignment-polyline pairs from command line
                    A.Ed.WriteMessage("\n\n--- Chọn các cặp Alignment-Polyline ---");
                    var pairs = new List<AlignmentPolylinePair>();

                    for (int i = 0; i < alignmentNumber; i++)
                    {
                        A.Ed.WriteMessage($"\n\n=== Cặp {i + 1}/{alignmentNumber} ===");

                        // Select turn alignment
                        ObjectId turnAlignmentId = UserInput.GAlignmentId($"\nChọn alignment rẽ phải {i + 1}: ");
                        if (turnAlignmentId == ObjectId.Null)
                        {
                            A.Ed.WriteMessage($"\n{WARNING_INDICATOR} Bỏ qua cặp {i + 1}");
                            continue;
                        }

                        var turnAlignment = tr.GetObject(turnAlignmentId, OpenMode.ForRead) as Alignment;
                        string turnAlignmentName = turnAlignment?.Name ?? "Unknown";
                        A.Ed.WriteMessage($"\n✓ Alignment: {turnAlignmentName}");

                        // Check if alignment has profile
                        if (turnAlignment != null && turnAlignment.GetProfileIds().Count == 0)
                        {
                            A.Ed.WriteMessage($"\n{WARNING_INDICATOR} Cảnh báo: Alignment '{turnAlignmentName}' không có profile!");
                            A.Ed.WriteMessage($"\n{WARNING_INDICATOR} Bỏ qua cặp {i + 1}");
                            continue;
                        }

                        // Select polyline
                        ObjectId polylineId = UserInput.GObjId($"\nChọn polyline biên cho alignment {i + 1}: ");
                        if (polylineId == ObjectId.Null)
                        {
                            A.Ed.WriteMessage($"\n{WARNING_INDICATOR} Bỏ qua cặp {i + 1}");
                            continue;
                        }

                        var polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                        if (polyline == null || polyline.NumberOfVertices < 2)
                        {
                            A.Ed.WriteMessage($"\n{WARNING_INDICATOR} Cần chọn Polyline có ít nhất hai đỉnh. Bỏ qua cặp {i + 1}.");
                            continue;
                        }
                        if (pairs.Any(pair => pair.AlignmentId == turnAlignmentId))
                        {
                            A.Ed.WriteMessage($"\n{WARNING_INDICATOR} Alignment này đã được chọn. Bỏ qua cặp trùng.");
                            continue;
                        }
                        string polylineName = $"Polyline_{i + 1}";
                        A.Ed.WriteMessage($"\n✓ Polyline: {polylineName}");

                        // Add pair
                        pairs.Add(new AlignmentPolylinePair
                        {
                            AlignmentId = turnAlignmentId,
                            AlignmentName = turnAlignmentName,
                            PolylineId = polylineId,
                            PolylineName = polylineName
                        });

                        A.Ed.WriteMessage($"\n✓ Đã thêm cặp {i + 1}");
                    }

                    if (pairs.Count == 0)
                    {
                        return new ExecutionResult<CorridorFormData>
                        {
                            Success = false,
                            Message = "Không có cặp alignment-polyline nào được chọn."
                        };
                    }

                    A.Ed.WriteMessage($"\n\n✓ Tổng cộng: {pairs.Count} cặp hợp lệ");

                    // Create result
                    var formData = new CorridorFormData
                    {
                        AlignmentNumber = pairs.Count,
                        CorridorId = corridorId,
                        TargetId1 = targetId1,
                        TargetId2 = targetId2,
                        AssemblyId = assemblyId,
                        AssemblyName = assemblyName,
                        AlignmentPolylinePairs = pairs
                    };

                    return new ExecutionResult<CorridorFormData>
                    {
                        Success = true,
                        Data = formData
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new ExecutionResult<CorridorFormData>
                {
                    Success = false,
                    Message = $"Lỗi khi nhận input: {ex.Message}"
                };
            }
        }

        private static ExecutionResult<CorridorObjects> ValidateAndGetObjects(Transaction tr, CorridorFormData formData)
        {
            try
            {
                var corridor = tr.GetObject(formData.CorridorId, OpenMode.ForWrite) as Corridor;
                if (corridor == null)
                {
                    return new ExecutionResult<CorridorObjects>
                    {
                        Success = false,
                        Message = "Không thể lấy đối tượng corridor."
                    };
                }

                var alignment1 = tr.GetObject(formData.TargetId1, OpenMode.ForRead) as Alignment;
                var alignment2 = tr.GetObject(formData.TargetId2, OpenMode.ForRead) as Alignment;

                if (alignment1 == null || alignment2 == null)
                {
                    return new ExecutionResult<CorridorObjects>
                    {
                        Success = false,
                        Message = "Không thể lấy đối tượng target alignment."
                    };
                }

                return new ExecutionResult<CorridorObjects>
                {
                    Success = true,
                    Data = new CorridorObjects
                    {
                        Corridor = corridor,
                        Alignment1 = alignment1,
                        Alignment2 = alignment2,
                        AssemblyId = formData.AssemblyId,
                        AssemblyName = formData.AssemblyName
                    }
                };
            }
            catch (System.Exception ex)
            {
                return new ExecutionResult<CorridorObjects>
                {
                    Success = false,
                    Message = $"Lỗi khi validate và lấy objects: {ex.Message}"
                };
            }
        }

        private static void LogObjectInfo(CorridorObjects objects)
        {
            A.Ed.WriteMessage("\n\n================================================");
            A.Ed.WriteMessage("\n  THONG TIN CAU HINH");
            A.Ed.WriteMessage("\n================================================");
            A.Ed.WriteMessage($"\nCorridor: {objects.Corridor.Name}");
            A.Ed.WriteMessage($"Target 1: {objects.Alignment1.Name}");
            A.Ed.WriteMessage($"Target 2: {objects.Alignment2.Name}");
            A.Ed.WriteMessage($"Assembly: {objects.AssemblyName}");
            A.Ed.WriteMessage("\n================================================\n");
        }

        private static ExecutionResult ProcessAlignmentPolylinePairs(Transaction tr, CorridorFormData formData, CorridorObjects objects)
        {
            int successCount = 0;
            var errors = new List<string>();

            A.Ed.WriteMessage("\n\n================================================");
            A.Ed.WriteMessage("\n  BAT DAU TAO CORRIDOR");
            A.Ed.WriteMessage("\n================================================");

            // ====== STEP 1: SELECT SURFACE ONCE FOR ALL CORRIDORS ======
            A.Ed.WriteMessage("\n\n--- Cấu hình chung cho tất cả corridors ---");
            A.Ed.WriteMessage("\n--- Chọn Surface (dùng chung) ---");

            ObjectIdCollection sharedSurfaceTargets = new ObjectIdCollection();
            try
            {
                ObjectId surfaceId = ObjectId.Null;
                
                // Check if we have a saved surface from previous run
                if (!string.IsNullOrEmpty(_savedSurfaceName))
                {
                    A.Ed.WriteMessage($"\n📋 Đã tìm thấy surface đã lưu: {_savedSurfaceName}");
                    
                    // Try to find the surface by name
                    surfaceId = FindSurfaceByName(tr, _savedSurfaceName);
                    
                    if (surfaceId != ObjectId.Null)
                    {
                        // Ask user if they want to use the saved surface
                        PromptKeywordOptions pko = new PromptKeywordOptions(
                            $"\nSử dụng surface đã lưu '{_savedSurfaceName}'? [Yes/No] <Yes>: ");
                        pko.Keywords.Add("Yes");
                        pko.Keywords.Add("No");
                        pko.Keywords.Default = "Yes";
                        pko.AllowNone = true;
                        
                        PromptResult pkr = A.Ed.GetKeywords(pko);
                        
                        if (pkr.Status == PromptStatus.Cancel || 
                            (pkr.StringResult == "No"))
                        {
                            // User wants to select a new surface
                            surfaceId = ObjectId.Null;
                            A.Ed.WriteMessage("\n→ Chọn surface mới...");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n✅ Sử dụng surface đã lưu: {_savedSurfaceName}");
                        }
                    }
                    else
                    {
                        A.Ed.WriteMessage($"\n⚠️ Không tìm thấy surface '{_savedSurfaceName}' trong bản vẽ.");
                    }
                }
                
                // If no saved surface or user wants to select new one
                if (surfaceId == ObjectId.Null)
                {
                    surfaceId = UserInput.GSurfaceId("\nChọn surface để target taluy (dùng chung cho tất cả): ");
                }
                
                if (surfaceId != ObjectId.Null)
                {
                    sharedSurfaceTargets.Add(surfaceId);
                    CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                    if (surface != null)
                    {
                        // Save surface name for next run
                        _savedSurfaceName = surface.Name;
                        A.Ed.WriteMessage($"\n✅ Đã chọn surface: {surface.Name}");
                    }
                }
                else
                {
                    A.Ed.WriteMessage("\n⚠️ Không chọn surface. Sẽ bỏ qua surface targets.");
                }
            }
            catch (System.Exception surfaceException)
            {
                A.Ed.WriteMessage($"\n⚠️ Lỗi khi chọn surface: {surfaceException.Message}");
                A.Ed.WriteMessage("\n⚠️ Tiếp tục không có surface targets.");
            }

            // ====== STEP 2: CONFIGURE TARGET MAPPING ONCE ======
            A.Ed.WriteMessage("\n\n--- Cấu hình Target Mapping (dùng chung) ---");

            TargetMappingConfiguration? targetMapping = null;
            bool useFormConfig = true;

            // Get target alignments and profiles
            var profiles1 = objects.Alignment1.GetProfileIds();
            var profiles2 = objects.Alignment2.GetProfileIds();
            ObjectId profileId_1 = profiles1.Count > 0 ? profiles1[0] : ObjectId.Null;
            ObjectId profileId_2 = profiles2.Count > 0 ? profiles2[0] : ObjectId.Null;

            ObjectIdCollection sharedAlignmentTargets = new ObjectIdCollection { objects.Alignment1.Id, objects.Alignment2.Id };
            ObjectIdCollection sharedProfileTargets = new ObjectIdCollection();
            if (profileId_1 != ObjectId.Null) sharedProfileTargets.Add(profileId_1);
            if (profileId_2 != ObjectId.Null) sharedProfileTargets.Add(profileId_2);

            // We need to get a sample subassembly target collection to show the form
            // We'll use the first alignment-polyline pair to get the assembly targets
            if (formData.AlignmentNumber > 0)
            {
                var firstPair = formData.AlignmentPolylinePairs[0];
                var firstAlignment = tr.GetObject(firstPair.AlignmentId, OpenMode.ForRead) as Alignment;
                var firstPolyline = tr.GetObject(firstPair.PolylineId, OpenMode.ForRead) as Polyline;

                if (firstAlignment != null && firstPolyline != null && firstAlignment.GetProfileIds().Count > 0)
                {
                    // Create a temporary baseline region to get subassembly targets
                    Baseline? tempBaseline = null;
                    try
                    {
                        A.Ed.WriteMessage($"\n📋 Lấy thông tin subassembly targets từ assembly '{objects.AssemblyName}'...");

                        // Create temporary corridor structures to analyze assembly
                        ObjectId profileId = firstAlignment.GetProfileIds()[0];
                        Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;

                        string tempBaselineName = "TEMP_BL_" + Guid.NewGuid().ToString().Substring(0, 8);
                        tempBaseline = objects.Corridor.Baselines.Add(tempBaselineName, firstAlignment.Id, profileId);

                        // Get station range
                        var (startStation, endStation) = GetTurnRegionStations(firstAlignment);

                        string tempRegionName = "TEMP_RG_" + Guid.NewGuid().ToString().Substring(0, 8);
                        BaselineRegion tempRegion = tempBaseline.BaselineRegions.Add(tempRegionName, objects.AssemblyId, startStation, endStation);

                        // Get subassembly targets
                        SubassemblyTargetInfoCollection sampleTargets = tempRegion.GetTargets();
                        string targetSignature = string.Join("|", Enumerable.Range(0, sampleTargets.Count)
                            .Select(index => $"{sampleTargets[index].SubassemblyName}:{sampleTargets[index].TargetType}"));

                        A.Ed.WriteMessage($"\n✅ Tìm thấy {sampleTargets.Count} subassembly targets trong assembly.");

                        if (sampleTargets.Count > 0)
                        {
                            // Prepare shared polyline targets (we'll use first one as sample, will be replaced per corridor)
                            ObjectIdCollection samplePolylineTargets = new ObjectIdCollection { firstPolyline.Id };

                            // Show form to configure target mapping
                            if (useFormConfig)
                            {
                                try
                                {
                                    // Check if we have saved target mapping from previous run
                                    bool useSavedMapping = false;
                                    if (_savedTargetMapping != null && _savedTargetMapping.UseFormConfig &&
                                        _savedTargetMapping.AssemblyId == objects.AssemblyId &&
                                        _savedTargetMapping.TargetSignature == targetSignature &&
                                        _savedTargetMapping.SavedConnections != null && _savedTargetMapping.SavedConnections.Count > 0)
                                    {
                                        A.Ed.WriteMessage($"\n📋 Đã tìm thấy cấu hình target đã lưu ({_savedTargetMapping.SavedConnections.Count} targets)");
                                        
                                        // Ask user if they want to use the saved configuration
                                        PromptKeywordOptions pko = new PromptKeywordOptions(
                                            $"\nSử dụng cấu hình target đã lưu? [Yes/No] <Yes>: ");
                                        pko.Keywords.Add("Yes");
                                        pko.Keywords.Add("No");
                                        pko.Keywords.Default = "Yes";
                                        pko.AllowNone = true;
                                        
                                        PromptResult pkr = A.Ed.GetKeywords(pko);
                                        
                                        // Only open form if user explicitly says "No"
                                        // Otherwise (Enter, "Y", "Yes", or any other) use saved config
                                        if (pkr.Status == PromptStatus.Cancel ||
                                            (pkr.StringResult == "No"))
                                        {
                                            A.Ed.WriteMessage("\n→ Mở form cấu hình target mới...");
                                        }
                                        else
                                        {
                                            useSavedMapping = true;
                                            targetMapping = _savedTargetMapping;
                                            A.Ed.WriteMessage("\n✅ Sử dụng cấu hình target đã lưu.");
                                        }
                                    }

                                    // If not using saved configuration, show the form
                                    if (!useSavedMapping)
                                    {
                                        A.Ed.WriteMessage("\n\n=== Mở form cấu hình Target (dùng chung cho tất cả corridors) ===");

                                        using var targetConfigForm = new SubassemblyTargetConfigForm(
                                            sampleTargets,
                                            sharedAlignmentTargets,
                                            sharedProfileTargets,
                                            sharedSurfaceTargets,
                                            samplePolylineTargets);

                                        var dialogResult = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(targetConfigForm);

                                        if (dialogResult == DialogResult.OK && targetConfigForm.ConfigurationApplied)
                                        {
                                            A.Ed.WriteMessage("\n✅ Người dùng đã cấu hình target mapping.");

                                            // Store the target mapping configuration
                                            targetMapping = new TargetMappingConfiguration
                                            {
                                                AssemblyId = objects.AssemblyId,
                                                TargetSignature = targetSignature,
                                                UseFormConfig = true,
                                                TargetConnections = null,
                                                // Create SavedConnections (primitives only) for next run
                                                SavedConnections = targetConfigForm.TargetConnections
                                                    .Select(tc => new SavedTargetConnection
                                                    {
                                                        SubassemblyIndex = tc.SubassemblyIndex,
                                                        TargetGroupId = tc.TargetGroupId,
                                                        TargetOption = (int)tc.TargetOption
                                                    }).ToList()
                                            };

                                            // Save for next run
                                            _savedTargetMapping = targetMapping;
                                            A.Ed.WriteMessage("\n💾 Đã lưu cấu hình target cho lần thực hiện sau.");
                                        }
                                        else
                                        {
                                            A.Ed.WriteMessage("\n⚠️ Người dùng đã hủy form. Sẽ sử dụng cấu hình mặc định.");
                                            targetMapping = new TargetMappingConfiguration
                                            {
                                                UseFormConfig = false,
                                                TargetConnections = null
                                            };
                                        }
                                    }
                                }
                                catch (System.Exception formEx)
                                {
                                    A.Ed.WriteMessage($"\n❌ Lỗi khi hiển thị form: {formEx.Message}");
                                    A.Ed.WriteMessage("\n⚠️ Sẽ sử dụng cấu hình mặc định.");
                                    targetMapping = new TargetMappingConfiguration
                                    {
                                        UseFormConfig = false,
                                        TargetConnections = null
                                    };
                                }
                            }
                            else
                            {
                                targetMapping = new TargetMappingConfiguration
                                {
                                    UseFormConfig = false,
                                    TargetConnections = null
                                };
                            }
                        }

                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n❌ Lỗi khi lấy thông tin subassembly targets: {ex.Message}");
                        A.Ed.WriteMessage("\n⚠️ Sẽ sử dụng cấu hình mặc định.");
                        targetMapping = new TargetMappingConfiguration
                        {
                            UseFormConfig = false,
                            TargetConnections = null
                        };
                    }
                    finally
                    {
                        if (tempBaseline != null)
                            objects.Corridor.Baselines.Remove(tempBaseline);
                    }
                }
            }

            // If no target mapping configured, use default
            if (targetMapping == null)
            {
                targetMapping = new TargetMappingConfiguration
                {
                    UseFormConfig = false,
                    TargetConnections = null
                };
            }

            // ====== STEP 3: PROCESS EACH ALIGNMENT-POLYLINE PAIR ======
            A.Ed.WriteMessage("\n\n=== Bắt đầu tạo từng corridor ===");

            for (int i = 0; i < formData.AlignmentNumber; i++)
            {
                var pair = formData.AlignmentPolylinePairs[i];

                A.Ed.WriteMessage($"\n\n--- Xử lý cặp {i + 1}/{formData.AlignmentNumber} ---");

                try
                {
                    var result = ProcessSinglePairWithSharedConfig(
                        tr,
                        pair,
                        objects,
                        i,
                        sharedAlignmentTargets,
                        sharedProfileTargets,
                        sharedSurfaceTargets,
                        targetMapping);

                    if (result.Success)
                    {
                        successCount++;
                        A.Ed.WriteMessage($"\n{SUCCESS_INDICATOR} Cặp {i + 1}: Thành công!");
                    }
                    else
                    {
                        errors.Add($"Cặp {i + 1}: {result.Message}");
                        A.Ed.WriteMessage($"\n{ERROR_INDICATOR} Cặp {i + 1}: {result.Message}");
                    }
                }
                catch (System.Exception ex)
                {
                    var errorMsg = $"Lỗi: {ex.Message}";
                    errors.Add($"Cặp {i + 1}: {errorMsg}");
                    A.Ed.WriteMessage($"\n{ERROR_INDICATOR} Cặp {i + 1}: {errorMsg}");
                }
            }

            // Rebuild corridor after all baselines and regions have been created
            if (successCount > 0)
            {
                try
                {
                    A.Ed.WriteMessage("\n\n--- Rebuild Corridor ---");
                    // Upgrade corridor to write mode before rebuild
                    if (!objects.Corridor.IsWriteEnabled)
                        objects.Corridor.UpgradeOpen();
                    objects.Corridor.Rebuild();
                    A.Ed.WriteMessage($"\n{SUCCESS_INDICATOR} Đã rebuild corridor thành công.");
                }
                catch (System.Exception rebuildEx)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        Message = $"Rebuild corridor thất bại; hủy các thay đổi của lệnh: {rebuildEx.Message}"
                    };
                }
            }

            // Report results
            A.Ed.WriteMessage($"\n\n================================================");
            A.Ed.WriteMessage($"\n  KET QUA");
            A.Ed.WriteMessage($"\n================================================");
            A.Ed.WriteMessage($"\nĐã hoàn thành: {successCount}/{formData.AlignmentNumber} corridor rẽ phải");

            if (errors.Count > 0)
            {
                A.Ed.WriteMessage($"\nCó {errors.Count} lỗi xảy ra:");
                foreach (var error in errors)
                {
                    A.Ed.WriteMessage($"\n  - {error}");
                }
            }

            A.Ed.WriteMessage("\n================================================\n");

            var overallSuccess = successCount > 0;
            var message = overallSuccess
                ? $"Đã tạo thành công {successCount}/{formData.AlignmentNumber} corridor."
                : "Không có corridor nào được tạo thành công.";

            return new ExecutionResult
            {
                Success = overallSuccess,
                Message = message
            };
        }

        private static ExecutionResult ProcessSinglePairWithSharedConfig(
            Transaction tr,
            AlignmentPolylinePair pair,
            CorridorObjects objects,
            int pairIndex,
            ObjectIdCollection sharedAlignmentTargets,
            ObjectIdCollection sharedProfileTargets,
            ObjectIdCollection sharedSurfaceTargets,
            TargetMappingConfiguration targetMapping)
        {
            try
            {
                var alignment = tr.GetObject(pair.AlignmentId, OpenMode.ForRead) as Alignment;
                var polyline = tr.GetObject(pair.PolylineId, OpenMode.ForRead) as Polyline;

                if (alignment == null)
                {
                    return new ExecutionResult { Success = false, Message = "Không thể lấy đối tượng alignment" };
                }

                if (polyline == null)
                {
                    return new ExecutionResult { Success = false, Message = "Không thể lấy đối tượng polyline" };
                }

                A.Ed.WriteMessage($"\nAlignment: {alignment.Name}");
                A.Ed.WriteMessage($"Polyline: {pair.PolylineName}");

                // Validate alignment has profile
                if (alignment.GetProfileIds().Count == 0)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        Message = $"Alignment '{alignment.Name}' không có profile"
                    };
                }

                // Call the corridor creation method with shared configuration
                try
                {
                    A.Ed.WriteMessage($"\n→ Tạo corridor với cấu hình đã thiết lập...");

                    TaoCooridorDuongDoThiWithSharedConfig(
                        alignment,
                        objects.Corridor,
                        polyline,
                        objects.AssemblyId,
                        objects.AssemblyName,
                        sharedAlignmentTargets,
                        sharedProfileTargets,
                        sharedSurfaceTargets,
                        targetMapping);

                    A.Ed.WriteMessage($"\n{SUCCESS_INDICATOR} Đã thiết lập targets cho subassemblies");
                    return new ExecutionResult { Success = true };
                }
                catch (System.Exception ex)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        Message = $"Lỗi khi tạo corridor: {ex.Message}"
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Message = $"Lỗi khi xử lý cặp: {ex.Message}"
                };
            }
        }

        // ========== MOVED METHODS FROM UtilitiesC3D ==========

        private static (double Start, double End) GetTurnRegionStations(Alignment alignment)
        {
            if (alignment.Entities.Count == 0)
                throw new InvalidOperationException($"Alignment '{alignment.Name}' không có hình học.");

            // Preserve the original convention: the second entity is the turn.
            // A single-entity alignment uses that entity's complete station range.
            var entity = alignment.Entities.GetEntityByOrder(alignment.Entities.Count > 1 ? 1 : 0);
            var range = entity switch
            {
                AlignmentLine line => (Start: line.StartStation, End: line.EndStation),
                AlignmentArc arc => (Start: arc.StartStation, End: arc.EndStation),
                AlignmentSpiral spiral => (Start: spiral.StartStation, End: spiral.EndStation),
                _ => throw new InvalidOperationException($"Chưa hỗ trợ đoạn rẽ kiểu {entity.EntityType}.")
            };
            if (!double.IsFinite(range.Start) || !double.IsFinite(range.End) || range.End <= range.Start)
                throw new InvalidOperationException($"Phạm vi lý trình không hợp lệ: {alignment.Name}.");
            return range;
        }

        public static void TaoCooridorDuongDoThiWithSharedConfig(
            Alignment alignment,
            Corridor corridor,
            Polyline polyline,
            ObjectId assemblyId,
            string assemblyName,
            ObjectIdCollection sharedAlignmentTargets,
            ObjectIdCollection sharedProfileTargets,
            ObjectIdCollection sharedSurfaceTargets,
            TargetMappingConfiguration targetMapping)
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                Corridor corridorWrite = tr.GetObject(corridor.Id, OpenMode.ForWrite) as Corridor ?? corridor;

                //get station from alignment
                var (startStation, endStation) = GetTurnRegionStations(alignment);

                // set start and end point for corridor region
                var profileIds = alignment.GetProfileIds();
                if (profileIds.Count == 0)
                {
                    throw new InvalidOperationException($"Alignment '{alignment.Name}' không có profile.");
                }
                ObjectId profileId = profileIds[0];
                Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                if (profile == null || startStation < profile.StartingStation || endStation > profile.EndingStation)
                    throw new InvalidOperationException($"Profile không phủ hết đoạn rẽ của alignment '{alignment.Name}'.");

                //check baseline exist
                string baselineName = "BL-" + alignment.Name + "-" + profile?.Name;
                foreach (Baseline BL in corridorWrite.Baselines)
                {
                    if (BL.Name == baselineName)
                    {
                        corridorWrite.Baselines.Remove(corridorWrite.Baselines[baselineName]);
                        break;
                    }
                }

                // then add it again
                Baseline baselineAdd = corridorWrite.Baselines.Add("BL-" + alignment.Name + "-" + profile?.Name, alignment.Id, profileId);

                // Use the provided assembly
                A.Ed.WriteMessage($"\nSử dụng assembly: {assemblyName}");

                string regionName = "RG-" + alignment.Name + "-" + startStation.ToString("F2") + "-" + endStation.ToString("F2");
                BaselineRegion baselineRegion = baselineAdd.BaselineRegions.Add(regionName, assemblyId, startStation, endStation);

                //set frequency for assembly
                SetFrequencySection(baselineRegion, 2);

                // Prepare target collections (with THIS corridor's polyline)
                ObjectIdCollection TagetIds_0 = sharedAlignmentTargets;
                ObjectIdCollection TagetIds_1 = sharedProfileTargets;
                ObjectIdCollection TagetIds_2 = sharedSurfaceTargets;
                ObjectIdCollection TagetIds_3 = new ObjectIdCollection { polyline.Id };

                //get target for subassembly
                SubassemblyTargetInfoCollection subassemblyTargetInfoCollection = baselineRegion.GetTargets();

                A.Ed.WriteMessage($"\nSố lượng subassembly targets: {subassemblyTargetInfoCollection.Count}");

                // Check if we have any targets available
                bool hasAnyTargets = (TagetIds_0.Count > 0) || (TagetIds_1.Count > 0) || (TagetIds_2.Count > 0) || (TagetIds_3.Count > 0);

                if (!hasAnyTargets)
                {
                    A.Ed.WriteMessage("\n‼ CẢNH BÁO: Không có target objects nào để gắn kết!");
                    A.Ed.WriteMessage($"\nℹ️ Đã tạo corridor region '{regionName}' không có targets.");
                    tr.Commit();
                    return;
                }

                // Apply target configuration based on mapping
                // First check for SavedConnections (primitives, survives across transactions)
                if (targetMapping.UseFormConfig && targetMapping.SavedConnections != null && targetMapping.SavedConnections.Count > 0)
                {
                    A.Ed.WriteMessage("\n=== Áp dụng cấu hình từ Form/Saved Config ===");
                    ApplyTargetConfigurationFromSaved(baselineRegion, targetMapping.SavedConnections,
                        TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                }
                else if (targetMapping.UseFormConfig && targetMapping.TargetConnections != null && targetMapping.TargetConnections.Count > 0)
                {
                    A.Ed.WriteMessage("\n=== Áp dụng cấu hình từ Form (đã lưu) ===");
                    ApplyTargetConfigurationFromForm(baselineRegion, targetMapping.TargetConnections,
                        TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                }
                else
                {
                    A.Ed.WriteMessage("\n=== Áp dụng cấu hình Target mặc định ===");
                    ApplySimpleAndReliableTargetConfiguration(baselineRegion, TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                }

                // Verification step
                var finalTargetCollection = baselineRegion.GetTargets();
                int configuredTargets = 0;
                for (int i = 0; i < finalTargetCollection.Count; i++)
                {
                    var targetInfo = finalTargetCollection[i];
                    if (targetInfo.TargetIds.Count > 0)
                    {
                        configuredTargets++;
                    }
                }

                A.Ed.WriteMessage($"\n✅ Đã tạo corridor region '{regionName}' với {configuredTargets}/{finalTargetCollection.Count} targets được cấu hình.");
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                tr.Abort();
                throw;
            }
            catch (System.Exception generalEx)
            {
                A.Ed.WriteMessage($"\nLỗi: {generalEx.Message}");
                tr.Abort();
                throw;
            }
        }

        [Obsolete("Use TaoCooridorDuongDoThiWithSharedConfig instead for better performance")]
        public static void TaoCooridorDuongDoThiWithAssembly(Alignment alignment, Corridor corridor, Alignment alignment1, Alignment alignment2, Polyline polyline, ObjectId assemblyId, string assemblyName)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();

                //start here

                //get station from alignment
                double[] station = new double[alignment.Entities.Count];
                for (int i = 0; i < alignment.Entities.Count; i++)
                {
                    AlignmentEntity alignmentEntity = alignment.Entities.GetEntityByOrder(i);
                    switch (alignmentEntity.EntityType)
                    {
                        case AlignmentEntityType.Line:
                            {
                                AlignmentLine? alignmentLine = alignmentEntity as AlignmentLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                station[i] = alignmentLine.Length;
#pragma warning restore CS8602 // Dereference of apossibly null reference.
                                break;
                            }
                        case AlignmentEntityType.Arc:
                            {
                                AlignmentArc? alignmentArc = alignmentEntity as AlignmentArc;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                station[i] = alignmentArc.Length;
#pragma warning restore CS8602 // Dereference of apossibly null reference.
                                break;
                            }
                    }
                }
                double startStation = station[0];
                double endStation = station[0] + station[1];

                // set start and end point for corridor region
                if (alignment.GetProfileIds().Count == 0)
                {
                    A.Ed.WriteMessage($"\nLỗi: Alignment '{alignment.Name}' không có profile. Vui lòng tạo profile trước khi tạo corridor.");
                    return;
                }
                ObjectId profileId = alignment.GetProfileIds()[0];
                Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;

                Corridor corridorWrite = tr.GetObject(corridor.Id, OpenMode.ForWrite) as Corridor ?? corridor;

                //check baseline exist
#pragma warning disable CS8602 // Dereference of apossibly null reference.
                string baselineName = "BL-" + alignment.Name + "-" + profile.Name;
#pragma warning restore CS8602 // Dereference of apossibly null reference.
                foreach (Baseline BL in corridorWrite.Baselines)
                {
                    if (BL.Name == baselineName)
                    {
                        corridorWrite.Baselines.Remove(corridorWrite.Baselines[baselineName]);
                        break;
                    }
                }

                // then add it again
                Baseline baselineAdd = corridorWrite.Baselines.Add("BL-" + alignment.Name + "-" + profile.Name, alignment.Id, profileId);

                // Use the provided assembly
                A.Ed.WriteMessage($"\nSử dụng assembly: {assemblyName}");

                string regionName = "RG-" + alignment.Name + "-" + startStation.ToString("F2") + "-" + endStation.ToString("F2");
                BaselineRegion baselineRegion = baselineAdd.BaselineRegions.Add(regionName, assemblyId, startStation, endStation);

                //set frequency for assembly
                SetFrequencySection(baselineRegion, 2);

                // Verify target alignments have profiles
                if (alignment1.GetProfileIds().Count == 0)
                {
                    A.Ed.WriteMessage($"\nCảnh báo: Target alignment 1 '{alignment1.Name}' không có profile.");
                }
                if (alignment2.GetProfileIds().Count == 0)
                {
                    A.Ed.WriteMessage($"\nCảnh báo: Target alignment 2 '{alignment2.Name}' không có profile.");
                }

                ObjectId profileId_1 = alignment1.GetProfileIds().Count > 0 ? alignment1.GetProfileIds()[0] : ObjectId.Null;
                ObjectId profileId_2 = alignment2.GetProfileIds().Count > 0 ? alignment2.GetProfileIds()[0] : ObjectId.Null;

                // set target 0 - alignments
                ObjectIdCollection TagetIds_0 = [alignment1.Id, alignment2.Id];

                // set target 1 - profiles
                ObjectIdCollection TagetIds_1 = [];
                if (profileId_1 != ObjectId.Null) TagetIds_1.Add(profileId_1);
                if (profileId_2 != ObjectId.Null) TagetIds_1.Add(profileId_2);

                // set target 2 - surfaces (simplified implementation - just get available surfaces)
                ObjectIdCollection TagetIds_2 = [];
                try
                {
                    A.Ed.WriteMessage("\nTự động tìm kiếm surfaces có sẵn...");

                    // Get surfaces in model space
                    ObjectId surfaceId = UserInput.GSurfaceId("Chọn surface để target taluy");
                    if (surfaceId != ObjectId.Null)
                    {
                        TagetIds_2.Add(surfaceId);
                        CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                        if (surface != null)
                        {
                            A.Ed.WriteMessage($"\n  - {surface.Name} (được chọn thủ công)");
                        }
                    }

                }
                catch (System.Exception surfaceException)
                {
                    A.Ed.WriteMessage($"\nLỗi khi tìm kiếm surfaces: {surfaceException.Message}");
                }

                // set target 3 - polyline/other objects
                ObjectIdCollection TagetIds_3 = [polyline.Id];

                //get target for subassembly
                SubassemblyTargetInfoCollection subassemblyTargetInfoCollection = baselineRegion.GetTargets();

                A.Ed.WriteMessage($"\nSố lượng subassembly targets: {subassemblyTargetInfoCollection.Count}");

                // Debug: Show available target groups
                A.Ed.WriteMessage($"\nTarget Groups có sẵn:");
                A.Ed.WriteMessage($"  - Group 0 (Alignments): {TagetIds_0.Count} đối tượng");
                A.Ed.WriteMessage($"  - Group 1 (Profiles): {TagetIds_1.Count} đối tượng");
                A.Ed.WriteMessage($"  - Group 2 (Surfaces): {TagetIds_2.Count} đối tượng");
                A.Ed.WriteMessage($"  - Group 3 (Polylines/Other): {TagetIds_3.Count} đối tượng");

                // Check if we have any targets available
                bool hasAnyTargets = (TagetIds_0.Count > 0) || (TagetIds_1.Count > 0) || (TagetIds_2.Count > 0) || (TagetIds_3.Count > 0);

                if (!hasAnyTargets)
                {
                    A.Ed.WriteMessage("\n‼ CẢNH BÁO: Không có target objects nào để gắn kết!");
                    A.Ed.WriteMessage("\nℹ️ Sẽ tạo corridor region mà không có target assignments.");

                    // Still try to create the corridor region without targets
                    A.Ed.WriteMessage($"\nℹ️ Đã tạo corridor region '{regionName}' không có targets.");
                    tr.Commit();
                    return;
                }

                // *** NEW: Show form to configure targets ***
                A.Ed.WriteMessage("\n=== Mở form cấu hình Target ===");

                bool useFormConfig = true; // You can make this a user choice

                if (useFormConfig && subassemblyTargetInfoCollection.Count > 0)
                {
                    try
                    {
                        // Show the form
                        var targetConfigForm = new SubassemblyTargetConfigForm(
                            subassemblyTargetInfoCollection,
                            TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);

                        var dialogResult = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(targetConfigForm);

                        if (dialogResult == DialogResult.OK && targetConfigForm.ConfigurationApplied)
                        {
                            A.Ed.WriteMessage("\n✅ Người dùng đã áp dụng cấu hình từ form.");

                            // Apply the configuration from form IN THIS TRANSACTION CONTEXT
                            ApplyTargetConfigurationFromForm(baselineRegion, targetConfigForm.TargetConnections,
                                TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                        }
                        else
                        {
                            A.Ed.WriteMessage("\n⚠️ Người dùng đã hủy form. Áp dụng cấu hình mặc định...");
                            ApplySimpleAndReliableTargetConfiguration(baselineRegion, TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                        }
                    }
                    catch (System.Exception formEx)
                    {
                        A.Ed.WriteMessage($"\n❌ Lỗi khi hiển thị form: {formEx.Message}");
                        A.Ed.WriteMessage("\n⚠️ Áp dụng cấu hình mặc định...");
                        ApplySimpleAndReliableTargetConfiguration(baselineRegion, TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                    }
                }
                else
                {
                    // Fallback to automatic configuration
                    A.Ed.WriteMessage("\n=== Áp dụng cấu hình Target mặc định ===");
                    ApplySimpleAndReliableTargetConfiguration(baselineRegion, TagetIds_0, TagetIds_1, TagetIds_2, TagetIds_3);
                }

                // Add verification step
                A.Ed.WriteMessage("\n=== Kiểm tra kết quả Target configuration ===");
                var finalTargetCollection = baselineRegion.GetTargets();
                int configuredTargets = 0;
                for (int i = 0; i < finalTargetCollection.Count; i++)
                {
                    var targetInfo = finalTargetCollection[i];
                    if (targetInfo.TargetIds.Count >= 2)
                    {
                        configuredTargets++;
                        A.Ed.WriteMessage($"  ✅ Target {i}: {targetInfo.TargetIds.Count} targets configured");
                    }
                    else
                    {
                        A.Ed.WriteMessage($"  ⚠️ Target {i}: Chỉ có {targetInfo.TargetIds.Count} targets (cần cấu hình thủ công)");
                    }
                }

                if (configuredTargets == 0)
                {
                    A.Ed.WriteMessage($"\n‼ CẢNH BÁO: Không có target nào được thiết lập tự động!");
                    A.Ed.WriteMessage($"ℹ️ Bạn cần mở Corridor Properties và cấu hình targets thủ công cho subassemblies.");
                    A.Ed.WriteMessage($"ℹ️ Target groups có sẵn:");
                    A.Ed.WriteMessage($"    * Alignments: {TagetIds_0.Count} objects");
                    A.Ed.WriteMessage($"    * Profiles: {TagetIds_1.Count} objects");
                    A.Ed.WriteMessage($"    * Surfaces: {TagetIds_2.Count} objects");
                    A.Ed.WriteMessage($"    * Polylines/Other: {TagetIds_3.Count} objects");
                }
                else
                {
                    A.Ed.WriteMessage($"\n✅ Đã thiết lập targets tự động cho {configuredTargets}/{finalTargetCollection.Count} subassemblies");
                }

                A.Ed.WriteMessage($"\n✅ Đã tạo corridor region '{regionName}' thành công.");
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                tr.Abort();
            }
            catch (System.Exception generalEx)
            {
                A.Ed.WriteMessage($"\nLỗi: {generalEx.Message}");
                tr.Abort();
            }
        }

        public static void SetFrequencySection(BaselineRegion baselineRegion, int frequencyDistance)
        {
            baselineRegion.AppliedAssemblySetting.FrequencyAlongCurves = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongProfileCurves = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongSpirals = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongTangents = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongTargetCurves = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.AppliedAdjacentToOffsetTargetStartEnd = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtHorizontalGeometryPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtOffsetTargetGeometryPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtProfileGeometryPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtProfileHighLowPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtSuperelevationCriticalPoints = true;
        }

        /// <summary>
        /// Applies a simple and reliable target configuration
        /// Uses direct assignment approach with proper target type matching
        /// </summary>
        private static void ApplySimpleAndReliableTargetConfiguration(BaselineRegion baselineRegion,
            ObjectIdCollection TagetIds_0, ObjectIdCollection TagetIds_1, ObjectIdCollection TagetIds_2, ObjectIdCollection TagetIds_3)
        {
            try
            {
                var targetInfoCollection = baselineRegion.GetTargets();
                A.Ed.WriteMessage($"\nSố lượng subassembly targets cần cấu hình: {targetInfoCollection.Count}");

                if (targetInfoCollection.Count == 0)
                {
                    A.Ed.WriteMessage($"\nℹ️ Không có subassembly targets để cấu hình.");
                    return;
                }

                A.Ed.WriteMessage($"\nTarget objects available:");
                A.Ed.WriteMessage($"  - Alignments (TagetIds_0): {TagetIds_0.Count} objects");
                A.Ed.WriteMessage($"  - Profiles (TagetIds_1): {TagetIds_1.Count} objects");
                A.Ed.WriteMessage($"  - Surfaces (TagetIds_2): {TagetIds_2.Count} objects");
                A.Ed.WriteMessage($"  - Polylines/Other (TagetIds_3): {TagetIds_3.Count} objects");

                // First, show the subassembly types for debugging
                A.Ed.WriteMessage($"\n--- Debug: Subassembly Target Types ---");
                for (int i = 0; i < targetInfoCollection.Count; i++)
                {
                    try
                    {
                        string targetType = targetInfoCollection[i].TargetType.ToString();
                        A.Ed.WriteMessage($"Target {i}: {targetType}");
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"Target {i}: Could not get TargetType - {ex.Message}");
                    }
                }

                int successfulTargets = 0;
                int skippedTargets = 0;

                for (int i = 0; i < targetInfoCollection.Count; i++)
                {
                    var targetInfo = targetInfoCollection[i];

                    try
                    {
                        A.Ed.WriteMessage($"\n--- Processing Target {i} ---");
                        A.Ed.WriteMessage($"  Initial TargetIds count: {targetInfo.TargetIds.Count}");

                        // Get target type to determine appropriate objects
                        string targetType;
                        try
                        {
                            targetType = targetInfo.TargetType.ToString();
                            A.Ed.WriteMessage($"  Target type: {targetType}");
                        }
                        catch
                        {
                            A.Ed.WriteMessage($"  Could not determine target type, skipping...");
                            skippedTargets++;
                            continue;
                        }

                        // Select appropriate target collection based on type
                        ObjectIdCollection appropriateTargets = null;
                        string targetDescription = "";

                        if (targetType.Contains("Elevation"))
                        {
                            appropriateTargets = TagetIds_1; // Profiles for elevation
                            targetDescription = "Profiles";
                        }
                        else if (targetType.Contains("Offset"))
                        {
                            appropriateTargets = TagetIds_0; // Alignments for offset
                            targetDescription = "Alignments";
                        }
                        else if (targetType.Contains("Surface"))
                        {
                            appropriateTargets = TagetIds_2; // Surfaces for surface targets
                            targetDescription = "Surfaces";
                        }
                        else
                        {
                            // For unknown types, try polylines/other first, then fallback
                            appropriateTargets = TagetIds_3.Count > 0 ? TagetIds_3 :
                                                 TagetIds_0.Count > 0 ? TagetIds_0 :
                                                 TagetIds_1.Count > 0 ? TagetIds_1 : TagetIds_2;
                            targetDescription = "Mixed/Other";
                        }

                        if (appropriateTargets == null || appropriateTargets.Count == 0)
                        {
                            A.Ed.WriteMessage($"  ⚠️ Không có targets phù hợp cho {targetType}");
                            skippedTargets++;
                            continue;
                        }

                        // Create new ObjectIdCollection with appropriate targets
                        ObjectIdCollection newTargetIds = new ObjectIdCollection();

                        if (appropriateTargets.Count >= 2)
                        {
                            // Use first 2 targets from the appropriate collection
                            newTargetIds.Add(appropriateTargets[0]);
                            newTargetIds.Add(appropriateTargets[1]);
                            A.Ed.WriteMessage($"  Created collection with 2 {targetDescription} targets");
                        }
                        else if (appropriateTargets.Count == 1)
                        {
                            // Keep a single target without duplicate IDs
                            newTargetIds.Add(appropriateTargets[0]);
                            A.Ed.WriteMessage($"  Created collection with 1 {targetDescription} target");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"  ⚠️ Target {i}: Không có {targetDescription} targets khả dụng");
                            skippedTargets++;
                            continue;
                        }

                        // Direct assignment
                        A.Ed.WriteMessage($"  Assigning {targetDescription} collection directly to TargetIds...");
                        targetInfo.TargetIds = newTargetIds;
                        A.Ed.WriteMessage($"  ✅ Gán trực tiếp hoàn tất");
                        A.Ed.WriteMessage($"  Final TargetIds count: {targetInfo.TargetIds.Count}");

                        // Set properties AFTER targets are assigned
                        if (targetInfo.TargetIds.Count >= 2)
                        {
                            try
                            {
                                targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;
                                A.Ed.WriteMessage($"  ✅ Đặt TargetToOption thành Nearest");
                            }
                            catch (System.Exception propertyEx)
                            {
                                A.Ed.WriteMessage($"  Warning: Could not set TargetToOption - {propertyEx.Message}");
                            }
                        }

                        A.Ed.WriteMessage($"  ✅ Target {i} ({targetType}): Cấu hình thành công với {targetDescription}");
                        successfulTargets++;
                    }
                    catch (System.Exception targetException)
                    {
                        A.Ed.WriteMessage($"  ❌ Target {i}: Lỗi - {targetException.Message}");
                        throw;
                    }
                }

                A.Ed.WriteMessage($"\n--- Applying configuration to baseline region ---");
                A.Ed.WriteMessage($"Total processed targets: {successfulTargets}");
                A.Ed.WriteMessage($"Skipped targets: {skippedTargets}");

                // Apply the configuration to the baseline region
                try
                {
                    baselineRegion.SetTargets(targetInfoCollection);
                    A.Ed.WriteMessage($"\n✅ Gọi SetTargets() thành công!");
                    A.Ed.WriteMessage($"    - Đã xử lý: {successfulTargets}/{targetInfoCollection.Count} targets");
                    if (skippedTargets > 0)
                    {
                        A.Ed.WriteMessage($"    - Bỏ qua: {skippedTargets} targets (lỗi cấu hình)");
                    }
                }
                catch (System.Exception setTargetsException)
                {
                    A.Ed.WriteMessage($"\n❌ SetTargets() thất bại: {setTargetsException.Message}");
                    throw;
                }
            }
            catch (System.Exception generalException)
            {
                A.Ed.WriteMessage($"\nLỗi tổng quát trong cấu hình target: {generalException.Message}");
                throw;
            }
        }

        /// <summary>
        /// Apply target configuration from SavedConnections (primitives only)
        /// This can work across transactions since it doesn't depend on SubassemblyTargetInfo objects
        /// </summary>
        private static void ApplyTargetConfigurationFromSaved(BaselineRegion baselineRegion,
            List<SavedTargetConnection> savedConnections,
            ObjectIdCollection TagetIds_0, ObjectIdCollection TagetIds_1, ObjectIdCollection TagetIds_2, ObjectIdCollection TagetIds_3)
        {
            if (savedConnections.Count == 0)
            {
                A.Ed.WriteMessage("\n⚠️ Không có saved connections.");
                return;
            }

            try
            {
                // Get a fresh copy of targets from baseline region (in current transaction)
                var targetInfoCollection = baselineRegion.GetTargets();

                A.Ed.WriteMessage("\n=== Áp dụng cấu hình từ SavedConnections ===");
                A.Ed.WriteMessage($"\nSố lượng subassembly targets: {targetInfoCollection.Count}");
                A.Ed.WriteMessage($"\nSố lượng saved connections: {savedConnections.Count}");

                int successCount = 0;

                foreach (var savedConn in savedConnections)
                {
                    try
                    {
                        if (savedConn.SubassemblyIndex < 0 || savedConn.SubassemblyIndex >= targetInfoCollection.Count)
                        {
                            throw new InvalidOperationException($"Target index {savedConn.SubassemblyIndex} ngoài phạm vi.");
                        }

                        var targetInfo = targetInfoCollection[savedConn.SubassemblyIndex];
                        A.Ed.WriteMessage($"\n\nTarget {savedConn.SubassemblyIndex}:");

                        // Get appropriate target collection based on GroupId
                        ObjectIdCollection? selectedTargets = savedConn.TargetGroupId switch
                        {
                            0 => TagetIds_0,
                            1 => TagetIds_1,
                            2 => TagetIds_2,
                            3 => TagetIds_3,
                            _ => null
                        };

                        if (selectedTargets == null || selectedTargets.Count == 0)
                        {
                            A.Ed.WriteMessage($"\n  ⚠️ Không gắn kết (GroupId={savedConn.TargetGroupId})");
                            continue;
                        }

                        string groupName = savedConn.TargetGroupId switch
                        {
                            0 => "Alignments",
                            1 => "Profiles",
                            2 => "Surfaces",
                            3 => "Polylines",
                            _ => "Unknown"
                        };
                        A.Ed.WriteMessage($"\n  - Gắn kết với: {groupName} ({selectedTargets.Count} đối tượng)");

                        // Create NEW ObjectIdCollection
                        ObjectIdCollection newTargetIds = new ObjectIdCollection();

                        // Add new targets
                        if (selectedTargets.Count >= 2)
                        {
                            newTargetIds.Add(selectedTargets[0]);
                            newTargetIds.Add(selectedTargets[1]);
                        }
                        else if (selectedTargets.Count == 1)
                        {
                            newTargetIds.Add(selectedTargets[0]);
                            // A single target is valid; do not duplicate its ObjectId.
                        }

                        // Assign NEW collection
                        targetInfo.TargetIds = newTargetIds;

                        // Set target option
                        if (newTargetIds.Count > 1)
                            targetInfo.TargetToOption = (SubassemblyTargetToOption)savedConn.TargetOption;
                        A.Ed.WriteMessage($"\n  - Tùy chọn: {(SubassemblyTargetToOption)savedConn.TargetOption}");
                        A.Ed.WriteMessage($"\n  - TargetIds count: {targetInfo.TargetIds.Count}");

                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ❌ Lỗi khi áp dụng target {savedConn.SubassemblyIndex}: {ex.Message}");
                        throw;
                    }
                }

                // Apply to baseline region
                A.Ed.WriteMessage($"\n\n--- Gọi SetTargets() trên baseline region ---");
                baselineRegion.SetTargets(targetInfoCollection);

                A.Ed.WriteMessage($"\n✅ Đã áp dụng cấu hình từ saved connections cho {successCount}/{savedConnections.Count} targets.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi áp dụng saved connections: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Apply target configuration from form's TargetConnections
        /// This executes in the transaction context where BaselineRegion was created
        /// </summary>
        private static void ApplyTargetConfigurationFromForm(BaselineRegion baselineRegion,
            List<TargetConnection> targetConnections,
            ObjectIdCollection TagetIds_0, ObjectIdCollection TagetIds_1, ObjectIdCollection TagetIds_2, ObjectIdCollection TagetIds_3)
        {
            if (targetConnections.Count == 0)
            {
                A.Ed.WriteMessage("\n⚠️ Không có target connections từ form.");
                return;
            }

            try
            {
                // Get a fresh copy of targets from baseline region (in current transaction)
                var targetInfoCollection = baselineRegion.GetTargets();

                A.Ed.WriteMessage("\n=== Áp dụng cấu hình từ Form ===");
                A.Ed.WriteMessage($"\nSố lượng subassembly targets: {targetInfoCollection.Count}");
                A.Ed.WriteMessage($"\nSố lượng connections từ form: {targetConnections.Count}");

                int successCount = 0;

                foreach (var connection in targetConnections)
                {
                    try
                    {
                        // Validate index
                        if (connection.SubassemblyIndex < 0 || connection.SubassemblyIndex >= targetInfoCollection.Count)
                        {
                            A.Ed.WriteMessage($"\n⚠️ Target index {connection.SubassemblyIndex} ngoài phạm vi.");
                            continue;
                        }

                        var targetInfo = targetInfoCollection[connection.SubassemblyIndex];

                        A.Ed.WriteMessage($"\n\nTarget {connection.SubassemblyIndex}:");

                        // Get appropriate target collection based on user's choice
                        ObjectIdCollection? selectedTargets = connection.TargetGroupId switch
                        {
                            0 => TagetIds_0,
                            1 => TagetIds_1,
                            2 => TagetIds_2,
                            3 => TagetIds_3,
                            _ => null
                        };

                        if (selectedTargets == null || selectedTargets.Count == 0)
                        {
                            A.Ed.WriteMessage($"\n  ⚠️ Không gắn kết (GroupId={connection.TargetGroupId}, không có targets)");
                            continue;
                        }

                        string groupName = connection.TargetGroupId switch
                        {
                            0 => "Alignments",
                            1 => "Profiles",
                            2 => "Surfaces",
                            3 => "Polylines",
                            _ => "Unknown"
                        };

                        A.Ed.WriteMessage($"\n  - Gắn kết với: {groupName} ({selectedTargets.Count} đối tượng)");

                        // Create NEW ObjectIdCollection
                        ObjectIdCollection newTargetIds = new ObjectIdCollection();

                        // Add targets
                        if (selectedTargets.Count >= 2)
                        {
                            newTargetIds.Add(selectedTargets[0]);
                            newTargetIds.Add(selectedTargets[1]);
                        }
                        else if (selectedTargets.Count == 1)
                        {
                            newTargetIds.Add(selectedTargets[0]);
                            // A single target is valid; do not duplicate its ObjectId.
                        }

                        // Assign NEW collection
                        targetInfo.TargetIds = newTargetIds;

                        // Set target option
                        targetInfo.TargetToOption = connection.TargetOption;
                        A.Ed.WriteMessage($"\n  - Tùy chọn: {connection.TargetOption}");
                        A.Ed.WriteMessage($"\n  - TargetIds count: {targetInfo.TargetIds.Count}");

                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ❌ Lỗi khi áp dụng target {connection.SubassemblyIndex}: {ex.Message}");
                    }
                }

                // Apply to baseline region
                A.Ed.WriteMessage($"\n\n--- Gọi SetTargets() trên baseline region ---");
                baselineRegion.SetTargets(targetInfoCollection);

                A.Ed.WriteMessage($"\n✅ Đã áp dụng cấu hình từ form cho {successCount}/{targetConnections.Count} targets.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi áp dụng cấu hình từ form: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Helper method to find a surface by name in the current document
        /// </summary>
        private static ObjectId FindSurfaceByName(Transaction tr, string surfaceName)
        {
            try
            {
                var surfaceIds = A.Cdoc.GetSurfaceIds();
                foreach (ObjectId surfaceId in surfaceIds)
                {
                    CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                    if (surface != null && surface.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return surfaceId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️ Lỗi khi tìm surface '{surfaceName}': {ex.Message}");
            }
            return ObjectId.Null;
        }

        // Helper classes
        public class CorridorFormData
        {
            public int AlignmentNumber { get; set; }
            public ObjectId CorridorId { get; set; }
            public ObjectId TargetId1 { get; set; }
            public ObjectId TargetId2 { get; set; }
            public ObjectId AssemblyId { get; set; }
            public string AssemblyName { get; set; } = "";
            public List<AlignmentPolylinePair> AlignmentPolylinePairs { get; set; } = new();
        }

        public class AlignmentPolylinePair
        {
            public ObjectId AlignmentId { get; set; }
            public string AlignmentName { get; set; } = "";
            public ObjectId PolylineId { get; set; }
            public string PolylineName { get; set; } = "";
        }

        public class CorridorObjects
        {
            public Corridor Corridor { get; set; } = null!;
            public Alignment Alignment1 { get; set; } = null!;
            public Alignment Alignment2 { get; set; } = null!;
            public ObjectId AssemblyId { get; set; }
            public string AssemblyName { get; set; } = "";
        }

        public class TargetMappingConfiguration
        {
            public ObjectId AssemblyId { get; set; } = ObjectId.Null;
            public string TargetSignature { get; set; } = "";
            public bool UseFormConfig { get; set; }
            public List<TargetConnection>? TargetConnections { get; set; }
            
            // Saved version (primitives only) for reuse across transactions
            public List<SavedTargetConnection>? SavedConnections { get; set; }
        }

        /// <summary>
        /// Serializable version of TargetConnection (primitives only, no Civil 3D objects)
        /// </summary>
        public class SavedTargetConnection
        {
            public int SubassemblyIndex { get; set; }
            public int TargetGroupId { get; set; }
            public int TargetOption { get; set; } // SubassemblyTargetToOption as int
        }

        public class ExecutionResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
        }

        public class ExecutionResult<T> : ExecutionResult
        {
            public T Data { get; set; } = default!;
        }
    }
}

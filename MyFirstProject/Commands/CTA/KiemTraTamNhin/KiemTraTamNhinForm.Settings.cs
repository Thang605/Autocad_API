using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsColor = System.Drawing.Color;
using WinFormsFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    public partial class KiemTraTamNhinForm : Form
    {
        private void SaveCurrentSettings()
        {
            try
            {
                _lastSourceMode = rbSourceAlignment.Checked ? 0 : 1;

                if (cbbAlignment.SelectedItem is ObjectIdItem aItem)
                    _lastAlignmentId = aItem.Id;

                if (cbbProfile.SelectedItem is ObjectIdItem pItem)
                    _lastProfileId = pItem.Id;

                if (cbbSurface.SelectedItem is ObjectIdItem sItem)
                    _lastSurfaceId = sItem.Id;

                _lastPolylineId = _selectedPolylineId;

                if (cbbStandard.SelectedItem is SightStandardDefinition std)
                    _lastStandardName = std.Name;

                _lastSpeed = GetSelectedSpeed();

                _lastSightTypeIndex = rbStoppingSight.Checked ? 0 : 1;
                if (numReqSight != null)
                    _lastReqSight = numReqSight.Value;
                if (numInterval != null)
                    _lastInterval = numInterval.Value;
                if (numLaneOffset != null)
                    _lastLaneOffset = numLaneOffset.Value;
                if (numEyeHeight != null)
                    _lastEyeHeight = numEyeHeight.Value;
                if (numTargetHeight != null)
                    _lastTargetHeight = numTargetHeight.Value;

                if (chkCheckGrade != null)
                    _lastCheckGrade = chkCheckGrade.Checked;
                if (chkCheckClearance != null)
                    _lastCheckClearance = chkCheckClearance.Checked;
                if (chkCheck3DSurface != null)
                    _lastCheck3D = chkCheck3DSurface.Checked;
                if (chkFilterOnlyErrors != null)
                    _lastFilterOnlyErrors = chkFilterOnlyErrors.Checked;
                if (chkDrawEyePath != null)
                    _lastDrawEyePath = chkDrawEyePath.Checked;

                _lastFormSize = this.Size;
            }
            catch { }
        }

        private void RestoreLastSettings()
        {
            try
            {
                // 1. Khôi phục Tiêu chuẩn thiết kế
                if (!string.IsNullOrEmpty(_lastStandardName))
                {
                    for (int i = 0; i < cbbStandard.Items.Count; i++)
                    {
                        if (cbbStandard.Items[i] is SightStandardDefinition s && s.Name == _lastStandardName)
                        {
                            cbbStandard.SelectedIndex = i;
                            break;
                        }
                    }
                }

                // 2. Nạp danh sách vận tốc của tiêu chuẩn đã chọn vào cbbSpeed
                if (cbbStandard.SelectedItem is SightStandardDefinition curStd)
                {
                    cbbSpeed.Items.Clear();
                    foreach (var entry in curStd.Entries)
                    {
                        cbbSpeed.Items.Add(entry.Speed);
                    }
                }

                // 3. Khôi phục Vận tốc đã chọn
                _isUpdatingSpeed = true;
                int spdIdx = cbbSpeed.Items.IndexOf(_lastSpeed);
                if (spdIdx >= 0) cbbSpeed.SelectedIndex = spdIdx;
                else cbbSpeed.Text = _lastSpeed.ToString();
                _isUpdatingSpeed = false;

                // 4. Khôi phục Nguồn đường kiểm tra (Alignment vs Polyline)
                if (_lastSourceMode == 0)
                {
                    rbSourceAlignment.Checked = true;
                    rbSourcePolyline.Checked = false;
                    pnlAlignmentControls.Visible = true;
                    pnlPolylineControls.Visible = false;
                }
                else
                {
                    rbSourceAlignment.Checked = false;
                    rbSourcePolyline.Checked = true;
                    pnlAlignmentControls.Visible = false;
                    pnlPolylineControls.Visible = true;
                }

                // 5. Khôi phục Loại tầm nhìn
                if (_lastSightTypeIndex == 0)
                {
                    rbStoppingSight.Checked = true;
                    rbPassingSight.Checked = false;
                }
                else
                {
                    rbStoppingSight.Checked = false;
                    rbPassingSight.Checked = true;
                }

                // 6. Khôi phục chính xác các thông số số học (không bị bảng tra ghi đè)
                if (numInterval != null && _lastInterval >= numInterval.Minimum && _lastInterval <= numInterval.Maximum)
                    numInterval.Value = _lastInterval;
                if (numLaneOffset != null && _lastLaneOffset >= numLaneOffset.Minimum && _lastLaneOffset <= numLaneOffset.Maximum)
                    numLaneOffset.Value = _lastLaneOffset;
                if (numEyeHeight != null && _lastEyeHeight >= numEyeHeight.Minimum && _lastEyeHeight <= numEyeHeight.Maximum)
                    numEyeHeight.Value = _lastEyeHeight;
                if (numTargetHeight != null && _lastTargetHeight >= numTargetHeight.Minimum && _lastTargetHeight <= numTargetHeight.Maximum)
                    numTargetHeight.Value = _lastTargetHeight;
                if (numReqSight != null && _lastReqSight >= numReqSight.Minimum && _lastReqSight <= numReqSight.Maximum)
                    numReqSight.Value = _lastReqSight;

                // 7. Khôi phục CheckBoxes
                if (chkCheckGrade != null) chkCheckGrade.Checked = _lastCheckGrade;
                if (chkCheckClearance != null) chkCheckClearance.Checked = _lastCheckClearance;
                if (chkCheck3DSurface != null) chkCheck3DSurface.Checked = _lastCheck3D;
                if (chkFilterOnlyErrors != null) chkFilterOnlyErrors.Checked = _lastFilterOnlyErrors;
                if (chkDrawEyePath != null) chkDrawEyePath.Checked = _lastDrawEyePath;

                // 8. Khôi phục Profile cho Alignment và hiển thị Lý trình
                if (cbbAlignment.SelectedItem is ObjectIdItem aItem && !aItem.Id.IsNull)
                {
                    LoadProfilesForAlignment(aItem.Id, _lastProfileId);
                }

                if (_lastSourceMode == 1)
                {
                    UpdatePolylineStationDisplay();
                }

                // 9. Khôi phục kích thước form nếu có
                if (_lastFormSize.Width >= 1160 && _lastFormSize.Height >= 740)
                {
                    this.Size = _lastFormSize;
                }
            }
            catch { }
        }
    }
}

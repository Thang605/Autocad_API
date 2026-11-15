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
        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                MessageBox.Show("Chưa có kết quả để xuất Excel!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                string sourceName = rbSourceAlignment.Checked ? cbbAlignment.Text : "Polyline";
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.FileName = $"BaoCao_TamNhin_{sourceName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string stdName = cbbStandard.Text;
                        int speed = GetSelectedSpeed();
                        string sightName = rbStoppingSight.Checked ? "Tầm nhìn dừng xe (SSD)" : "Tầm nhìn vượt xe (PSD)";

                        SightDistanceEngine.ExportToExcel(_analysisResults, sfd.FileName, sourceName, stdName, speed, sightName);

                        var res = MessageBox.Show("Xuất file Excel thành công! Bạn có muốn mở file ngay bây giờ không?", "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (res == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}

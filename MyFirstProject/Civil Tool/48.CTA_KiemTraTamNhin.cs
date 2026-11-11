using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

[assembly: CommandClass(typeof(Civil3DCsharp.CTA_KiemTraTamNhin_Cmd))]

namespace Civil3DCsharp
{
    #region Enums & Data Models

    public enum SightCheckStatus
    {
        Pass,       // Đạt yêu cầu
        Warning,    // Cảnh báo (tiệm cận hoặc cần dạt tầm nhìn bụng cong)
        Fail        // Không đạt (bị che khuất hoặc R không đủ)
    }

    public enum SightType
    {
        Stopping,   // Tầm nhìn dừng xe (SSD)
        Passing     // Tầm nhìn vượt xe (PSD)
    }

    /// <summary>
    /// Thông số tiêu chuẩn tầm nhìn theo từng cấp vận tốc
    /// </summary>
    public class SightStandardEntry
    {
        public int Speed { get; set; }                          // km/h
        public double StoppingSightDist { get; set; }           // m (Tầm nhìn dừng xe tối thiểu S_d)
        public double PassingSightDist { get; set; }            // m (Tầm nhìn vượt xe tối thiểu S_v)
        public double MinConvexRadius { get; set; }             // m (R cong lồi thông thường)
        public double MinConvexRadiusLimit { get; set; }        // m (R cong lồi giới hạn)
        public double MinConcaveRadius { get; set; }            // m (R cong lõm thông thường)
        public double MinConcaveRadiusLimit { get; set; }       // m (R cong lõm giới hạn)
        public double MinHorizontalRadius { get; set; }         // m (R cong nằm giới hạn)
        public double MinHorizontalRadiusNormal { get; set; }   // m (R cong nằm thông thường)
        public double MaxGrade { get; set; }                    // % Độ dốc dọc tối đa
        public double LaneWidth { get; set; } = 3.50;           // m Bề rộng làn xe
        public double DefaultLaneOffset => Math.Round(LaneWidth / 2.0, 2); // m Khoảng cách từ tim đến tâm làn xe
        public double FrictionFactor { get; set; }              // Hệ số ma sát bám dọc f
    }

    /// <summary>
    /// Tiêu chuẩn tầm nhìn thiết kế đường
    /// </summary>
    public class SightStandardDefinition
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double EyeHeight { get; set; } = 1.25;           // Chiều cao mắt h1 (m)
        public double TargetHeightStopping { get; set; } = 0.10; // Chiều cao chướng ngại vật dừng xe h2 (m)
        public double TargetHeightPassing { get; set; } = 1.25;  // Chiều cao xe ngược chiều khi vượt h2 (m)
        public List<SightStandardEntry> Entries { get; set; } = new List<SightStandardEntry>();

        public SightStandardEntry? GetEntry(int speed)
        {
            if (Entries == null || Entries.Count == 0) return null;

            var match = Entries.FirstOrDefault(e => e.Speed == speed);
            if (match != null) return match;

            var sorted = Entries.OrderBy(e => e.Speed).ToList();
            if (speed <= sorted.First().Speed)
            {
                var first = sorted.First();
                return new SightStandardEntry
                {
                    Speed = speed,
                    StoppingSightDist = first.StoppingSightDist,
                    PassingSightDist = first.PassingSightDist,
                    MinConvexRadius = first.MinConvexRadius,
                    MinConvexRadiusLimit = first.MinConvexRadiusLimit,
                    MinConcaveRadius = first.MinConcaveRadius,
                    MinConcaveRadiusLimit = first.MinConcaveRadiusLimit,
                    MinHorizontalRadius = first.MinHorizontalRadius,
                    MinHorizontalRadiusNormal = first.MinHorizontalRadiusNormal,
                    MaxGrade = first.MaxGrade,
                    LaneWidth = first.LaneWidth,
                    FrictionFactor = first.FrictionFactor
                };
            }

            if (speed >= sorted.Last().Speed)
            {
                var last = sorted.Last();
                return new SightStandardEntry
                {
                    Speed = speed,
                    StoppingSightDist = last.StoppingSightDist,
                    PassingSightDist = last.PassingSightDist,
                    MinConvexRadius = last.MinConvexRadius,
                    MinConvexRadiusLimit = last.MinConvexRadiusLimit,
                    MinConcaveRadius = last.MinConcaveRadius,
                    MinConcaveRadiusLimit = last.MinConcaveRadiusLimit,
                    MinHorizontalRadius = last.MinHorizontalRadius,
                    MinHorizontalRadiusNormal = last.MinHorizontalRadiusNormal,
                    MaxGrade = last.MaxGrade,
                    LaneWidth = last.LaneWidth,
                    FrictionFactor = last.FrictionFactor
                };
            }

            // Nội suy tuyến tính giữa 2 mốc kề nhau
            var eLower = sorted.Last(e => e.Speed < speed);
            var eUpper = sorted.First(e => e.Speed > speed);

            double factor = (double)(speed - eLower.Speed) / (eUpper.Speed - eLower.Speed);

            return new SightStandardEntry
            {
                Speed = speed,
                StoppingSightDist = Math.Round(eLower.StoppingSightDist + factor * (eUpper.StoppingSightDist - eLower.StoppingSightDist), 1),
                PassingSightDist = (eLower.PassingSightDist > 0 && eUpper.PassingSightDist > 0)
                    ? Math.Round(eLower.PassingSightDist + factor * (eUpper.PassingSightDist - eLower.PassingSightDist), 1)
                    : Math.Max(eLower.PassingSightDist, eUpper.PassingSightDist),
                MinConvexRadius = Math.Round(eLower.MinConvexRadius + factor * (eUpper.MinConvexRadius - eLower.MinConvexRadius), 0),
                MinConvexRadiusLimit = Math.Round(eLower.MinConvexRadiusLimit + factor * (eUpper.MinConvexRadiusLimit - eLower.MinConvexRadiusLimit), 0),
                MinConcaveRadius = Math.Round(eLower.MinConcaveRadius + factor * (eUpper.MinConcaveRadius - eLower.MinConcaveRadius), 0),
                MinConcaveRadiusLimit = Math.Round(eLower.MinConcaveRadiusLimit + factor * (eUpper.MinConcaveRadiusLimit - eLower.MinConcaveRadiusLimit), 0),
                MinHorizontalRadius = Math.Round(eLower.MinHorizontalRadius + factor * (eUpper.MinHorizontalRadius - eLower.MinHorizontalRadius), 0),
                MinHorizontalRadiusNormal = Math.Round(eLower.MinHorizontalRadiusNormal + factor * (eUpper.MinHorizontalRadiusNormal - eLower.MinHorizontalRadiusNormal), 0),
                MaxGrade = Math.Round(eLower.MaxGrade + factor * (eUpper.MaxGrade - eLower.MaxGrade), 1),
                LaneWidth = Math.Round(eLower.LaneWidth + factor * (eUpper.LaneWidth - eLower.LaneWidth), 2),
                FrictionFactor = Math.Round(eLower.FrictionFactor + factor * (eUpper.FrictionFactor - eLower.FrictionFactor), 2)
            };
        }
    }

    /// <summary>
    /// Kết quả kiểm tra tại từng vị trí lý trình
    /// </summary>
    public class SightCheckItem
    {
        public double Station { get; set; }
        public string StationFormatted => $"Km{Math.Floor(Station / 1000):0}+{Station % 1000:000.00}";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double GradePercent { get; set; }             // Độ dốc dọc %
        public double HorizontalRadius { get; set; }        // Bán kính cong nằm (0 nếu đoạn thẳng)
        public double VerticalRadius { get; set; }          // Bán kính cong đứng (0 nếu không thuộc cong đứng)
        public bool IsConvexVertical { get; set; }          // True: cong lồi, False: cong lõm
        public double RequiredSight { get; set; }           // Tầm nhìn yêu cầu (m)
        public double ActualSight { get; set; }             // Tầm nhìn thực tế khả kiến (m)
        public double ClearanceWidthM { get; set; }         // Bề rộng dạt tầm nhìn bụng cong M (m)
        public SightCheckStatus Status { get; set; }
        public string Detail { get; set; } = "";
        public Point3d EyePoint { get; set; }
        public Point3d TargetPoint { get; set; }
        public Point3d? ObstructionPoint { get; set; }      // Điểm bị che khuất (nếu có)
    }

    #endregion

    #region Standard Repository

    public static class SightStandardRepo
    {
        private static readonly List<SightStandardDefinition> _standards = new List<SightStandardDefinition>();

        static SightStandardRepo()
        {
            // 1. TCVN 4054:2005 - Đường ô tô thiết kế
            var tcvn4054 = new SightStandardDefinition
            {
                Name = "TCVN 4054:2005",
                Description = "TCVN 4054:2005 - Đường Ô Tô (Bảng 11, 15, 17, 19)",
                EyeHeight = 1.25,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.25,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 20, StoppingSightDist = 20, PassingSightDist = 60, MinConvexRadius = 200, MinConvexRadiusLimit = 200, MinConcaveRadius = 200, MinConcaveRadiusLimit = 100, MinHorizontalRadius = 15, MinHorizontalRadiusNormal = 30, MaxGrade = 10.0, LaneWidth = 3.0, FrictionFactor = 0.42 },
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 30, PassingSightDist = 100, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 60, MaxGrade = 9.0, LaneWidth = 3.0, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 40, PassingSightDist = 150, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 60, MinHorizontalRadiusNormal = 125, MaxGrade = 8.0, LaneWidth = 3.25, FrictionFactor = 0.38 },
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 75, PassingSightDist = 300, MinConvexRadius = 4000, MinConvexRadiusLimit = 2500, MinConcaveRadius = 1500, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 125, MinHorizontalRadiusNormal = 250, MaxGrade = 6.0, LaneWidth = 3.50, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 100, PassingSightDist = 450, MinConvexRadius = 5000, MinConvexRadiusLimit = 4000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 250, MinHorizontalRadiusNormal = 500, MaxGrade = 5.0, LaneWidth = 3.50, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 150, PassingSightDist = 600, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 5000, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 400, MinHorizontalRadiusNormal = 1000, MaxGrade = 4.0, LaneWidth = 3.75, FrictionFactor = 0.29 },
                    new SightStandardEntry { Speed = 120, StoppingSightDist = 210, PassingSightDist = 750, MinConvexRadius = 17000, MinConvexRadiusLimit = 11000, MinConcaveRadius = 6000, MinConcaveRadiusLimit = 4000, MinHorizontalRadius = 600, MinHorizontalRadiusNormal = 1500, MaxGrade = 3.0, LaneWidth = 3.75, FrictionFactor = 0.28 }
                }
            };

            // 2. TCVN 13592:2022 - Đường Đô thị
            var tcvn13592 = new SightStandardDefinition
            {
                Name = "TCVN 13592:2022",
                Description = "TCVN 13592:2022 - Đường Đô Thị (Bảng 16, 23, 26)",
                EyeHeight = 1.00,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.20,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 30, PassingSightDist = 100, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 60, MaxGrade = 9.0, LaneWidth = 3.00, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 45, PassingSightDist = 150, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 60, MinHorizontalRadiusNormal = 125, MaxGrade = 8.0, LaneWidth = 3.25, FrictionFactor = 0.38 },
                    new SightStandardEntry { Speed = 50, StoppingSightDist = 60, PassingSightDist = 220, MinConvexRadius = 2500, MinConvexRadiusLimit = 1500, MinConcaveRadius = 1000, MinConcaveRadiusLimit = 700, MinHorizontalRadius = 100, MinHorizontalRadiusNormal = 150, MaxGrade = 7.0, LaneWidth = 3.50, FrictionFactor = 0.35 },
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 75, PassingSightDist = 300, MinConvexRadius = 4000, MinConvexRadiusLimit = 2500, MinConcaveRadius = 1500, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 125, MinHorizontalRadiusNormal = 250, MaxGrade = 6.0, LaneWidth = 3.50, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 110, PassingSightDist = 450, MinConvexRadius = 5000, MinConvexRadiusLimit = 4000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 250, MinHorizontalRadiusNormal = 500, MaxGrade = 5.0, LaneWidth = 3.75, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 160, PassingSightDist = 600, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 5000, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 400, MinHorizontalRadiusNormal = 1000, MaxGrade = 4.0, LaneWidth = 3.75, FrictionFactor = 0.29 }
                }
            };

            // 3. TCVN 5729:2012 - Đường Cao tốc
            var tcvn5729 = new SightStandardDefinition
            {
                Name = "TCVN 5729:2012",
                Description = "TCVN 5729:2012 - Đường Cao Tốc (Bảng 4 & Bảng 6)",
                EyeHeight = 1.25,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.25,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 75, PassingSightDist = 300, MinConvexRadius = 2000, MinConvexRadiusLimit = 1500, MinConcaveRadius = 2000, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 200, MinHorizontalRadiusNormal = 400, MaxGrade = 6.0, LaneWidth = 3.75, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 110, PassingSightDist = 450, MinConvexRadius = 4500, MinConvexRadiusLimit = 3000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 400, MinHorizontalRadiusNormal = 700, MaxGrade = 6.0, LaneWidth = 3.75, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 160, PassingSightDist = 600, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 4500, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 700, MinHorizontalRadiusNormal = 1200, MaxGrade = 5.0, LaneWidth = 3.75, FrictionFactor = 0.29 },
                    new SightStandardEntry { Speed = 120, StoppingSightDist = 210, PassingSightDist = 750, MinConvexRadius = 17000, MinConvexRadiusLimit = 12000, MinConcaveRadius = 6000, MinConcaveRadiusLimit = 5000, MinHorizontalRadius = 1000, MinHorizontalRadiusNormal = 2000, MaxGrade = 4.0, LaneWidth = 3.75, FrictionFactor = 0.28 }
                }
            };

            // 4. TCVN 10380:2014 - Đường Giao thông nông thôn
            var tcvn10380 = new SightStandardDefinition
            {
                Name = "TCVN 10380:2014",
                Description = "TCVN 10380:2014 - Đường GTNT (Bảng 7, 9)",
                EyeHeight = 1.25,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.25,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 15, StoppingSightDist = 15, PassingSightDist = 45, MinConvexRadius = 150, MinConvexRadiusLimit = 100, MinConcaveRadius = 150, MinConcaveRadiusLimit = 100, MinHorizontalRadius = 15, MinHorizontalRadiusNormal = 15, MaxGrade = 12.0, LaneWidth = 2.50, FrictionFactor = 0.45 },
                    new SightStandardEntry { Speed = 20, StoppingSightDist = 20, PassingSightDist = 60, MinConvexRadius = 200, MinConvexRadiusLimit = 150, MinConcaveRadius = 200, MinConcaveRadiusLimit = 100, MinHorizontalRadius = 15, MinHorizontalRadiusNormal = 30, MaxGrade = 11.0, LaneWidth = 2.75, FrictionFactor = 0.42 },
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 30, PassingSightDist = 100, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 60, MaxGrade = 10.0, LaneWidth = 3.00, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 40, PassingSightDist = 150, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 60, MinHorizontalRadiusNormal = 125, MaxGrade = 9.0, LaneWidth = 3.00, FrictionFactor = 0.38 }
                }
            };

            // 5. AASHTO Green Book
            var aashto = new SightStandardDefinition
            {
                Name = "AASHTO Green Book",
                Description = "AASHTO - A Policy on Geometric Design of Highways and Streets",
                EyeHeight = 1.08,
                TargetHeightStopping = 0.60,
                TargetHeightPassing = 1.08,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 35, PassingSightDist = 120, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 50, MaxGrade = 9.0, LaneWidth = 3.00, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 50, PassingSightDist = 170, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 50, MinHorizontalRadiusNormal = 100, MaxGrade = 8.0, LaneWidth = 3.30, FrictionFactor = 0.38 },
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 85, PassingSightDist = 345, MinConvexRadius = 4000, MinConvexRadiusLimit = 2500, MinConcaveRadius = 1500, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 110, MinHorizontalRadiusNormal = 220, MaxGrade = 6.0, LaneWidth = 3.50, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 130, PassingSightDist = 540, MinConvexRadius = 5000, MinConvexRadiusLimit = 4000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 210, MinHorizontalRadiusNormal = 450, MaxGrade = 5.0, LaneWidth = 3.60, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 185, PassingSightDist = 670, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 5000, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 370, MinHorizontalRadiusNormal = 900, MaxGrade = 4.0, LaneWidth = 3.60, FrictionFactor = 0.29 },
                    new SightStandardEntry { Speed = 120, StoppingSightDist = 250, PassingSightDist = 790, MinConvexRadius = 17000, MinConvexRadiusLimit = 12000, MinConcaveRadius = 6000, MinConcaveRadiusLimit = 5000, MinHorizontalRadius = 540, MinHorizontalRadiusNormal = 1400, MaxGrade = 3.5, LaneWidth = 3.60, FrictionFactor = 0.28 }
                }
            };

            _standards.Add(tcvn4054);
            _standards.Add(tcvn13592);
            _standards.Add(tcvn5729);
            _standards.Add(tcvn10380);
            _standards.Add(aashto);
        }

        public static List<SightStandardDefinition> GetAllStandards() => _standards;
    }

    /// <summary>
    /// Hộp thoại tra cứu và hiển thị toàn bộ bảng tiêu chuẩn thiết kế tầm nhìn
    /// </summary>
    public class BangTraTieuChuanTamNhinForm : System.Windows.Forms.Form
    {
        public SightStandardDefinition? SelectedStandard { get; private set; }
        public SightStandardEntry? SelectedEntry { get; private set; }

        private System.Windows.Forms.DataGridView dgv;
        private System.Windows.Forms.ComboBox cbbStd;

        public BangTraTieuChuanTamNhinForm(string defaultStandardName, int defaultSpeed)
        {
            this.Text = "BẢNG TRA CỨU TIÊU CHUẨN THIẾT KẾ TẦM NHÌN (TCVN & AASHTO)";
            this.Size = new System.Drawing.Size(1050, 480);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Regular);
            this.MinimizeBox = false;

            var pnlTop = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Top, Height = 48, Padding = new System.Windows.Forms.Padding(10, 8, 10, 8) };
            this.Controls.Add(pnlTop);

            var lbl = new System.Windows.Forms.Label { Text = "Chọn tiêu chuẩn để xem:", Location = new System.Drawing.Point(10, 14), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold) };
            pnlTop.Controls.Add(lbl);

            cbbStd = new System.Windows.Forms.ComboBox { Location = new System.Drawing.Point(170, 11), Width = 260, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
            cbbStd.DataSource = SightStandardRepo.GetAllStandards();
            cbbStd.DisplayMember = "Name";
            cbbStd.SelectedIndexChanged += (s, e) => LoadData();
            pnlTop.Controls.Add(cbbStd);

            var btnApply = new System.Windows.Forms.Button
            {
                Text = "✔ Chọn áp dụng thông số hàng này",
                Location = new System.Drawing.Point(445, 10),
                Width = 240,
                Height = 28,
                BackColor = System.Drawing.Color.FromArgb(28, 64, 112),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold)
            };
            btnApply.Click += (s, e) => ApplySelection();
            pnlTop.Controls.Add(btnApply);

            dgv = new System.Windows.Forms.DataGridView
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = System.Drawing.Color.White,
                AlternatingRowsDefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { BackColor = System.Drawing.Color.FromArgb(248, 250, 252) }
            };
            dgv.DoubleClick += (s, e) => ApplySelection();
            this.Controls.Add(dgv);

            // Chọn tiêu chuẩn mặc định
            for (int i = 0; i < cbbStd.Items.Count; i++)
            {
                if (((SightStandardDefinition)cbbStd.Items[i]).Name == defaultStandardName)
                {
                    cbbStd.SelectedIndex = i;
                    break;
                }
            }

            LoadData();

            // Chọn hàng theo speed
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                if (dgv.Rows[r].Tag is SightStandardEntry entry && entry.Speed == defaultSpeed)
                {
                    dgv.Rows[r].Selected = true;
                    break;
                }
            }
        }

        private void LoadData()
        {
            if (cbbStd.SelectedItem is not SightStandardDefinition std) return;

            dgv.Columns.Clear();
            dgv.Columns.Add("colSpeed", "V_tk (km/h)");
            dgv.Columns.Add("colStopping", "S dừng min (m)");
            dgv.Columns.Add("colPassing", "S vượt min (m)");
            dgv.Columns.Add("colConvex", "R lồi tt (m)");
            dgv.Columns.Add("colConvexLim", "R lồi gh (m)");
            dgv.Columns.Add("colConcave", "R lõm tt (m)");
            dgv.Columns.Add("colConcaveLim", "R lõm gh (m)");
            dgv.Columns.Add("colRHoriz", "R nằm gh (m)");
            dgv.Columns.Add("colMaxGrade", "i_max (%)");
            dgv.Columns.Add("colLane", "Làn xe (m)");
            dgv.Columns.Add("colOffset", "Offset tâm (m)");
            dgv.Columns.Add("colEye", "Mắt h1 (m)");
            dgv.Columns.Add("colTgt", "Vật h2 (m)");

            foreach (var ent in std.Entries)
            {
                int rIdx = dgv.Rows.Add(
                    ent.Speed,
                    ent.StoppingSightDist,
                    ent.PassingSightDist > 0 ? ent.PassingSightDist.ToString() : "-",
                    ent.MinConvexRadius,
                    ent.MinConvexRadiusLimit,
                    ent.MinConcaveRadius,
                    ent.MinConcaveRadiusLimit,
                    ent.MinHorizontalRadius,
                    $"{ent.MaxGrade:F1}%",
                    $"{ent.LaneWidth:F2}",
                    $"{ent.DefaultLaneOffset:F2}",
                    $"{std.EyeHeight:F2}",
                    $"{std.TargetHeightStopping:F2}"
                );
                dgv.Rows[rIdx].Tag = ent;
            }

            foreach (System.Windows.Forms.DataGridViewColumn col in dgv.Columns)
            {
                col.HeaderCell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            }
            dgv.AutoResizeColumns();
        }

        private void ApplySelection()
        {
            if (cbbStd.SelectedItem is SightStandardDefinition std && dgv.CurrentRow?.Tag is SightStandardEntry ent)
            {
                SelectedStandard = std;
                SelectedEntry = ent;
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
        }
    }

    #endregion

    #region Calculation Engine

    public static class SightDistanceEngine
    {
        /// <summary>
        /// Tính Tầm nhìn dừng xe (Stopping Sight Distance - SSD) có xét đến ảnh hưởng của độ dốc dọc i (%)
        /// Sd = (V * t / 3.6) + V^2 / [254 * (f +- i/100)]
        /// </summary>
        public static double CalculateStoppingSightDistance(double speedKmH, double gradePercent, double frictionFactor, double reactionTimeSec = 2.5)
        {
            double v = speedKmH;
            double t = reactionTimeSec;
            double f = frictionFactor;
            double gradeDecimal = gradePercent / 100.0;

            // Quãng đường phản ứng của tài xế
            double dReaction = (v * t) / 3.6;

            // Quãng đường phanh dừng (mẫu số tránh âm hoặc chia cho 0)
            double denominator = 254.0 * (f + gradeDecimal);
            if (denominator < 10.0) denominator = 10.0;

            double dBraking = (v * v) / denominator;
            double sd = dReaction + dBraking;

            return Math.Round(sd, 2);
        }

        /// <summary>
        /// Tính khoảng cách dạt tầm nhìn bụng đường cong nằm (Middle Ordinate M / Clearance):
        /// M = R * [1 - cos(28.65° * S / R)]
        /// </summary>
        public static double CalculateHorizontalClearance(double radius, double sightDistance)
        {
            if (radius <= 0 || sightDistance <= 0) return 0.0;
            if (radius > 10000) return 0.0; // Bán kính quá lớn xem như thẳng

            // Góc ở tâm nửa cung alpha/2 tính bằng radian = S / (2 * R)
            double halfAngleRad = sightDistance / (2.0 * radius);
            if (halfAngleRad > Math.PI / 2.0) halfAngleRad = Math.PI / 2.0;

            double m = radius * (1.0 - Math.Cos(halfAngleRad));
            return Math.Round(m, 2);
        }

        /// <summary>
        /// Kiểm tra tia ngắm 3D từ mắt tài xế đến mục tiêu xem có bị cắt bởi bề mặt địa hình TIN hay không
        /// </summary>
        public static bool Check3DSightRay(CivSurface? surface, Point3d eyePt, Point3d targetPt, out Point3d? obstructionPoint)
        {
            obstructionPoint = null;
            if (surface == null) return true; // Không chọn bề mặt thì coi như tia ngắm thông

            // Lấy 25 điểm mẫu dọc theo tia ngắm giữa mắt và mục tiêu
            int numSamples = 25;
            double dx = (targetPt.X - eyePt.X) / numSamples;
            double dy = (targetPt.Y - eyePt.Y) / numSamples;
            double dz = (targetPt.Z - eyePt.Z) / numSamples;

            for (int i = 1; i < numSamples; i++)
            {
                double px = eyePt.X + i * dx;
                double py = eyePt.Y + i * dy;
                double rayZ = eyePt.Z + i * dz;

                try
                {
                    double surfaceZ = surface.FindElevationAtXY(px, py);
                    // Nếu địa hình cao hơn tia ngắm + 0.05m thì bị chắn
                    if (surfaceZ > rayZ + 0.05)
                    {
                        obstructionPoint = new Point3d(px, py, surfaceZ);
                        return false; // Bị che khuất
                    }
                }
                catch
                {
                    // Điểm nằm ngoài boundary của surface -> bỏ qua
                }
            }

            return true; // Tia ngắm thông thoáng
        }

        /// <summary>
        /// Phân tích toàn diện tầm nhìn cho Tuyến
        /// </summary>
        public static List<SightCheckItem> AnalyzeSightDistance(
            Alignment alignment,
            Profile? profile,
            CivSurface? surface,
            SightStandardDefinition standard,
            int speed,
            SightType sightType,
            double startStation,
            double endStation,
            double interval,
            double laneOffset,
            double customEyeHeight,
            double customTargetHeight,
            bool checkGradeEffect,
            bool checkHorizontalClearance,
            bool check3DSurface,
            double customRequiredSight = 0.0)
        {
            var results = new List<SightCheckItem>();
            var entry = standard.GetEntry(speed);
            if (entry == null) return results;

            double baseSight = (customRequiredSight > 0)
                ? customRequiredSight
                : ((sightType == SightType.Stopping) ? entry.StoppingSightDist : entry.PassingSightDist);
            double eyeH = (customEyeHeight > 0) ? customEyeHeight : standard.EyeHeight;
            double targetH = (customTargetHeight > 0) ? customTargetHeight : ((sightType == SightType.Stopping) ? standard.TargetHeightStopping : standard.TargetHeightPassing);

            // Thu thập thông tin các đường cong nằm trên Alignment
            var horizontalArcs = new List<dynamic>();
            try
            {
                foreach (AlignmentEntity ent in alignment.Entities)
                {
                    if (ent.EntityType == AlignmentEntityType.Arc)
                    {
                        horizontalArcs.Add(ent);
                    }
                }
            }
            catch { }

            // Duyệt từng vị trí lý trình
            for (double st = startStation; st <= endStation + 0.001; st += interval)
            {
                if (st > alignment.EndingStation) st = alignment.EndingStation;

                var item = new SightCheckItem
                {
                    Station = st,
                    RequiredSight = baseSight,
                    ActualSight = baseSight,
                    Status = SightCheckStatus.Pass
                };

                // 1. Lấy tọa độ tim đường và cao độ
                double cx = 0, cy = 0;
                alignment.PointLocation(st, 0, ref cx, ref cy);
                item.X = cx;
                item.Y = cy;

                double centerZ = 0;
                if (profile != null)
                {
                    try { centerZ = profile.ElevationAt(st); }
                    catch
                    {
                        if (surface != null)
                            try { centerZ = surface.FindElevationAtXY(cx, cy); } catch { }
                    }
                }
                else if (surface != null)
                {
                    try { centerZ = surface.FindElevationAtXY(cx, cy); } catch { }
                }
                item.Z = centerZ;

                // 2. Xác định độ dốc dọc tại lý trình
                double grade = 0;
                if (profile != null)
                {
                    try
                    {
                        double stNext = Math.Min(st + 1.0, alignment.EndingStation);
                        double z1 = profile.ElevationAt(st);
                        double z2 = profile.ElevationAt(stNext);
                        grade = ((z2 - z1) / (stNext - st)) * 100.0;
                    }
                    catch { }
                }
                item.GradePercent = Math.Round(grade, 2);

                // Nếu kiểm tra dừng xe và có xét độ dốc
                if (sightType == SightType.Stopping && checkGradeEffect)
                {
                    item.RequiredSight = CalculateStoppingSightDistance(speed, item.GradePercent, entry.FrictionFactor);
                }

                // 3. Xác định bán kính cong nằm tại lý trình
                double hRadius = 0;
                foreach (var arc in horizontalArcs)
                {
                    try
                    {
                        if (st >= arc.StartStation && st <= arc.EndStation)
                        {
                            hRadius = Math.Round((double)arc.Radius, 2);
                            break;
                        }
                    }
                    catch { }
                }
                item.HorizontalRadius = hRadius;

                // 4. Tính khoảng dạt tầm nhìn bụng đường cong M
                if (hRadius > 0 && checkHorizontalClearance)
                {
                    item.ClearanceWidthM = CalculateHorizontalClearance(hRadius, item.RequiredSight);
                    if (item.ClearanceWidthM > 2.0)
                    {
                        item.Status = SightCheckStatus.Warning;
                        item.Detail = $"Bụng đường cong R={hRadius:F0}m cần dạt chướng ngại vật M={item.ClearanceWidthM:F2}m";
                    }
                }

                // 5. Kiểm tra đường cong đứng (nếu có Profile)
                if (profile != null)
                {
                    try
                    {
                        if (profile.Entities != null)
                        {
                            foreach (dynamic ent in profile.Entities)
                            {
                                try
                                {
                                    string entType = ent.GetType().Name;
                                    if (entType.Contains("Tangent")) continue;

                                    double startSt = Convert.ToDouble(ent.StartStation);
                                    double endSt = Convert.ToDouble(ent.EndStation);

                                    if (st >= startSt - 0.5 && st <= endSt + 0.5)
                                    {
                                        double vR = 0;
                                        try { vR = Convert.ToDouble(ent.Radius); } catch { }
                                        if (vR <= 0)
                                        {
                                            // Ngoại suy R từ K và chiều dài
                                            try
                                            {
                                                double len = Convert.ToDouble(ent.Length);
                                                double kVal = Convert.ToDouble(ent.K);
                                                if (kVal > 0) vR = kVal * 100.0;
                                            }
                                            catch { }
                                        }

                                        if (vR > 0)
                                        {
                                            item.VerticalRadius = Math.Round(vR, 0);
                                            // Kiểm tra cong lồi hay lõm qua GradeIn / GradeOut hoặc EntType
                                            bool isConvex = true;
                                            try
                                            {
                                                double gIn = Convert.ToDouble(ent.GradeIn);
                                                double gOut = Convert.ToDouble(ent.GradeOut);
                                                isConvex = (gIn > gOut);
                                            }
                                            catch { }

                                            item.IsConvexVertical = isConvex;

                                            if (isConvex && vR < entry.MinConvexRadius)
                                            {
                                                item.Status = SightCheckStatus.Fail;
                                                item.Detail = $"Bán kính cong lồi R={vR:F0}m < {entry.MinConvexRadius:F0}m (Yêu cầu SSD)";
                                            }
                                            else if (!isConvex && vR < entry.MinConcaveRadius)
                                            {
                                                item.Status = SightCheckStatus.Warning;
                                                item.Detail = $"Bán kính cong lõm R={vR:F0}m < {entry.MinConcaveRadius:F0}m (Yêu cầu đèn pha)";
                                            }
                                            break;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                // 6. Tính toán điểm Mắt và Điểm Mục tiêu 3D
                double eyeX = 0, eyeY = 0;
                alignment.PointLocation(st, laneOffset, ref eyeX, ref eyeY);
                double eyeZ = item.Z + eyeH;
                item.EyePoint = new Point3d(eyeX, eyeY, eyeZ);

                double targetSt = Math.Min(st + item.RequiredSight, alignment.EndingStation);
                double targetX = 0, targetY = 0;
                alignment.PointLocation(targetSt, laneOffset, ref targetX, ref targetY);

                double targetGroundZ = item.Z;
                if (profile != null)
                {
                    try { targetGroundZ = profile.ElevationAt(targetSt); } catch { }
                }
                double targetZ = targetGroundZ + targetH;
                item.TargetPoint = new Point3d(targetX, targetY, targetZ);

                // 7. Kiểm tra tia ngắm 3D xuyên qua Surface (nếu có chọn Surface)
                if (surface != null && check3DSurface)
                {
                    Point3d? obstruction;
                    bool isClear = Check3DSightRay(surface, item.EyePoint, item.TargetPoint, out obstruction);
                    if (!isClear && obstruction.HasValue)
                    {
                        item.Status = SightCheckStatus.Fail;
                        item.ObstructionPoint = obstruction;

                        double distToObs = item.EyePoint.DistanceTo(obstruction.Value);
                        item.ActualSight = Math.Round(distToObs, 1);
                        item.Detail = $"Tia ngắm bị chắn bởi địa hình tại cách {item.ActualSight:F1}m (Yêu cầu: {item.RequiredSight:F1}m)";
                    }
                }

                if (string.IsNullOrEmpty(item.Detail))
                {
                    item.Detail = (item.Status == SightCheckStatus.Pass) ? "Đảm bảo tầm nhìn xe chạy an toàn" : "Cần lưu ý bố trí mặt cắt";
                }

                results.Add(item);
                if (st >= alignment.EndingStation) break;
            }

            return results;
        }

        /// <summary>
        /// Tạo Layer nếu chưa có
        /// </summary>
        public static ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return lt[layerName];

            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = layerName,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            ObjectId layerId = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return layerId;
        }

        /// <summary>
        /// Vẽ đường Polyline biểu diễn quỹ đạo di chuyển của mắt người lái xe (Offset từ tim tuyến / polyline một khoảng bằng laneOffset)
        /// </summary>
        public static ObjectId DrawDriverEyeTrajectory(
            Database db,
            Transaction tr,
            BlockTableRecord btr,
            Alignment? alignment,
            ObjectId polylineId,
            double laneOffset,
            double startStation,
            double endStation)
        {
            string layerEyePath = "C3D_QUY_DAO_MAT_LAI_XE";
            GetOrCreateLayer(db, tr, layerEyePath, 4); // Màu 4 = Cyan (Xanh lơ nổi bật)

            var eyePathPline = new Polyline
            {
                Layer = layerEyePath,
                ColorIndex = 4,
                LineWeight = LineWeight.LineWeight030
            };
            int idx = 0;

            if (alignment != null)
            {
                double step = 2.0;
                double curSt = startStation;
                while (curSt < endStation)
                {
                    double x = 0, y = 0;
                    alignment.PointLocation(curSt, laneOffset, ref x, ref y);
                    eyePathPline.AddVertexAt(idx++, new Point2d(x, y), 0, 0, 0);
                    curSt += step;
                }
                double ex = 0, ey = 0;
                alignment.PointLocation(endStation, laneOffset, ref ex, ref ey);
                eyePathPline.AddVertexAt(idx++, new Point2d(ex, ey), 0, 0, 0);
            }
            else if (!polylineId.IsNull && polylineId.IsValid)
            {
                var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                if (curve != null)
                {
                    double totalLen = curve.GetDistanceAtParameter(curve.EndParam);
                    double step = 2.0;
                    double s = 0.0;
                    while (s < totalLen)
                    {
                        Point3d pt = curve.GetPointAtDist(s);
                        Vector3d tangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(s));
                        double tLen = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
                        if (tLen > 1e-6)
                        {
                            Vector3d normal2d = new Vector3d(-tangent.Y / tLen, tangent.X / tLen, 0);
                            Point3d ptEye = pt + normal2d * laneOffset;
                            eyePathPline.AddVertexAt(idx++, new Point2d(ptEye.X, ptEye.Y), 0, 0, 0);
                        }
                        s += step;
                    }
                    Point3d ptEnd = curve.GetPointAtDist(totalLen);
                    Vector3d endTangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(totalLen));
                    double endTLen = Math.Sqrt(endTangent.X * endTangent.X + endTangent.Y * endTangent.Y);
                    if (endTLen > 1e-6)
                    {
                        Vector3d normal2d = new Vector3d(-endTangent.Y / endTLen, endTangent.X / endTLen, 0);
                        Point3d ptEye = ptEnd + normal2d * laneOffset;
                        eyePathPline.AddVertexAt(idx++, new Point2d(ptEye.X, ptEye.Y), 0, 0, 0);
                    }
                }
            }

            if (eyePathPline.NumberOfVertices > 1)
            {
                btr.AppendEntity(eyePathPline);
                tr.AddNewlyCreatedDBObject(eyePathPline, true);
                return eyePathPline.ObjectId;
            }
            return ObjectId.Null;
        }

        /// <summary>
        /// Vẽ tia ngắm và vùng dạt tầm nhìn lên CAD
        /// </summary>
        public static int DrawSightRaysToCAD(
            List<SightCheckItem> items, 
            bool drawOnlyNonPass,
            bool drawEyeTrajectory = true,
            ObjectId alignmentId = default,
            ObjectId polylineId = default,
            double laneOffset = 1.75,
            double startStation = 0,
            double endStation = 0,
            Alignment? alignment = null)
        {
            int count = 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                string layerClear = "C3D_TAM_NHIN_DAT";
                string layerBlocked = "C3D_TAM_NHIN_KHUAT";
                GetOrCreateLayer(db, tr, layerClear, 3);   // Xanh lá
                GetOrCreateLayer(db, tr, layerBlocked, 1); // Đỏ

                // 1. Vẽ đường Polyline biểu diễn quỹ đạo di chuyển của mắt người lái xe (Offset làn)
                if (drawEyeTrajectory)
                {
                    Alignment? alignToUse = alignment;
                    if (alignToUse == null && !alignmentId.IsNull && alignmentId.IsValid)
                    {
                        try { alignToUse = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment; } catch { }
                    }
                    if (alignToUse != null)
                    {
                        if (startStation == 0 && endStation == 0)
                        {
                            startStation = alignToUse.StartingStation;
                            endStation = alignToUse.EndingStation;
                        }
                    }
                    DrawDriverEyeTrajectory(db, tr, btr, alignToUse, polylineId, laneOffset, startStation, endStation);
                }

                foreach (var item in items)
                {
                    if (drawOnlyNonPass && item.Status == SightCheckStatus.Pass) continue;

                    string targetLayer = (item.Status == SightCheckStatus.Pass) ? layerClear : layerBlocked;
                    short colorIdx = (item.Status == SightCheckStatus.Pass) ? (short)3 : (item.Status == SightCheckStatus.Warning ? (short)2 : (short)1);

                    // 2. Vẽ tia ngắm phẳng trên mặt bằng CAD với cao độ Z = 0
                    var ptStart2D = new Point3d(item.EyePoint.X, item.EyePoint.Y, 0.0);
                    var ptEnd2D = new Point3d(item.TargetPoint.X, item.TargetPoint.Y, 0.0);

                    var line = new Line(ptStart2D, ptEnd2D)
                    {
                        Layer = targetLayer,
                        ColorIndex = colorIdx
                    };
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    count++;

                    // 3. Nếu bị chắn, vẽ điểm va chạm với cao độ Z = 0
                    if (item.ObstructionPoint.HasValue)
                    {
                        var circle = new Circle
                        {
                            Center = new Point3d(item.ObstructionPoint.Value.X, item.ObstructionPoint.Value.Y, 0.0),
                            Radius = 5.0,
                            Layer = layerBlocked,
                            ColorIndex = 1
                        };
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                    }
                }

                tr.Commit();
            }

            return count;
        }

        /// <summary>
        /// Đánh dấu các vị trí không đạt / cảnh báo lên mặt bằng tuyến CAD
        /// </summary>
        public static int DrawWarningMarksToCAD(List<SightCheckItem> items, Alignment align)
        {
            int count = 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                string layerMark = "C3D_TAM_NHIN_CANH_BAO";
                GetOrCreateLayer(db, tr, layerMark, 1);

                foreach (var item in items.Where(i => i.Status != SightCheckStatus.Pass))
                {
                    short colorIdx = (item.Status == SightCheckStatus.Fail) ? (short)1 : (short)2; // 1 = Đỏ, 2 = Vàng

                    // Vòng tròn tại tim tuyến
                    var circle = new Circle
                    {
                        Center = new Point3d(item.X, item.Y, 0),
                        Radius = 15.0,
                        Layer = layerMark,
                        ColorIndex = colorIdx
                    };
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);

                    // Crosshair chữ thập
                    var l1 = new Line(new Point3d(item.X - 20, item.Y, 0), new Point3d(item.X + 20, item.Y, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                    var l2 = new Line(new Point3d(item.X, item.Y - 20, 0), new Point3d(item.X, item.Y + 20, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                    btr.AppendEntity(l1);
                    btr.AppendEntity(l2);
                    tr.AddNewlyCreatedDBObject(l1, true);
                    tr.AddNewlyCreatedDBObject(l2, true);

                    // Text ghi chú vi phạm
                    var txt = new DBText
                    {
                        Position = new Point3d(item.X + 18, item.Y + 18, 0),
                        Height = 5.0,
                        TextString = $"[{item.StationFormatted}] {item.Detail}",
                        Layer = layerMark,
                        ColorIndex = colorIdx
                    };
                    btr.AppendEntity(txt);
                    tr.AddNewlyCreatedDBObject(txt, true);

                    count++;
                }

                tr.Commit();
            }

            return count;
        }

        /// <summary>
        /// Chuyển đổi một đường Polyline 2D thành Polyline3d bằng cách nội suy cao độ từ Surface
        /// </summary>
        public static ObjectId ConvertPolylineToPolyline3d(Polyline pline, CivSurface? surface, double sampleStep = 5.0)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            ObjectId poly3dId = ObjectId.Null;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                string layerName = "C3D_TIM_TUYEN_3D_POLYLINE";
                GetOrCreateLayer(db, tr, layerName, 4); // Màu Cyan

                double totalLength = pline.Length;
                var distSet = new SortedSet<double>();

                // 1. Thêm khoảng cách tại các đỉnh hiện tại của Polyline
                for (int i = 0; i < pline.NumberOfVertices; i++)
                {
                    double dist = pline.GetDistanceAtParameter(i);
                    distSet.Add(dist);
                }

                // 2. Thêm các điểm chia mẫu đều theo sampleStep
                for (double d = 0; d < totalLength; d += sampleStep)
                {
                    distSet.Add(d);
                }
                distSet.Add(totalLength);

                // 3. Tạo Point3dCollection với cao độ từ Surface (hoặc pline.Elevation)
                var ptColl = new Point3dCollection();
                foreach (double d in distSet)
                {
                    Point3d pt2d = pline.GetPointAtDist(d);
                    double z = pline.Elevation;
                    if (surface != null)
                    {
                        try { z = surface.FindElevationAtXY(pt2d.X, pt2d.Y); } catch { }
                    }
                    ptColl.Add(new Point3d(pt2d.X, pt2d.Y, z));
                }

                if (ptColl.Count > 1)
                {
                    var poly3d = new Polyline3d(Poly3dType.SimplePoly, ptColl, pline.Closed)
                    {
                        Layer = layerName,
                        ColorIndex = 4
                    };
                    poly3dId = btr.AppendEntity(poly3d);
                    tr.AddNewlyCreatedDBObject(poly3d, true);
                }

                tr.Commit();
            }

            return poly3dId;
        }

        /// <summary>
        /// Phân tích toàn diện tầm nhìn cho đối tượng Polyline hoặc Polyline3d
        /// </summary>
        public static List<SightCheckItem> AnalyzeSightDistanceForCurve(
            Curve curve,
            CivSurface? surface,
            SightStandardDefinition standard,
            int speed,
            SightType sightType,
            double startDist,
            double endDist,
            double interval,
            double laneOffset,
            double customEyeHeight,
            double customTargetHeight,
            bool checkGradeEffect,
            bool checkHorizontalClearance,
            bool check3DSurface,
            double customRequiredSight = 0.0)
        {
            var results = new List<SightCheckItem>();
            var entry = standard.GetEntry(speed);
            if (entry == null) return results;

            double baseSight = (customRequiredSight > 0)
                ? customRequiredSight
                : ((sightType == SightType.Stopping) ? entry.StoppingSightDist : entry.PassingSightDist);
            double eyeH = (customEyeHeight > 0) ? customEyeHeight : standard.EyeHeight;
            double targetH = (customTargetHeight > 0) ? customTargetHeight : ((sightType == SightType.Stopping) ? standard.TargetHeightStopping : standard.TargetHeightPassing);

            double totalLength = curve.GetDistanceAtParameter(curve.EndParam);
            if (endDist <= 0 || endDist > totalLength) endDist = totalLength;

            for (double s = startDist; s <= endDist + 0.001; s += interval)
            {
                if (s > totalLength) s = totalLength;

                var item = new SightCheckItem
                {
                    Station = s,
                    RequiredSight = baseSight,
                    ActualSight = baseSight,
                    Status = SightCheckStatus.Pass
                };

                // 1. Lấy tọa độ tim đường
                Point3d ptCenter = curve.GetPointAtDist(s);
                item.X = ptCenter.X;
                item.Y = ptCenter.Y;
                item.Z = ptCenter.Z;

                // 2. Lấy vector tiếp tuyến và pháp tuyến ngang
                Vector3d tangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(s));
                double tLen = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
                Vector3d normal2d = new Vector3d(0, 1, 0);
                if (tLen > 1e-6)
                {
                    double ux = tangent.X / tLen;
                    double uy = tangent.Y / tLen;
                    normal2d = new Vector3d(-uy, ux, 0); // Quay 90 độ sang trái/phải
                }

                // 3. Xác định độ dốc dọc i (%)
                double sNext = Math.Min(s + 1.0, totalLength);
                if (sNext > s)
                {
                    Point3d ptNext = curve.GetPointAtDist(sNext);
                    item.GradePercent = Math.Round(((ptNext.Z - ptCenter.Z) / (sNext - s)) * 100.0, 2);
                }

                if (sightType == SightType.Stopping && checkGradeEffect)
                {
                    item.RequiredSight = CalculateStoppingSightDistance(speed, item.GradePercent, entry.FrictionFactor);
                }

                // 4. Tính bán kính cong nằm tại lý trình
                double hRadius = 0;
                if (curve is Polyline pl)
                {
                    try
                    {
                        int seg = (int)pl.GetParameterAtDistance(s);
                        if (Math.Abs(pl.GetBulgeAt(seg)) > 1e-6)
                        {
                            var arc = pl.GetArcSegment2dAt(seg);
                            hRadius = Math.Round(arc.Radius, 2);
                        }
                    }
                    catch { }
                }

                if (hRadius <= 0)
                {
                    // Lấy bán kính qua 3 điểm (s - 5m, s, s + 5m)
                    try
                    {
                        double sA = Math.Max(0, s - 5.0);
                        double sC = Math.Min(totalLength, s + 5.0);
                        if (sC - sA >= 2.0)
                        {
                            Point3d pA = curve.GetPointAtDist(sA);
                            Point3d pB = ptCenter;
                            Point3d pC = curve.GetPointAtDist(sC);

                            double da = Math.Sqrt(Math.Pow(pC.X - pB.X, 2) + Math.Pow(pC.Y - pB.Y, 2));
                            double db = Math.Sqrt(Math.Pow(pC.X - pA.X, 2) + Math.Pow(pC.Y - pA.Y, 2));
                            double dc = Math.Sqrt(Math.Pow(pB.X - pA.X, 2) + Math.Pow(pB.Y - pA.Y, 2));
                            double area2 = Math.Abs((pB.X - pA.X) * (pC.Y - pA.Y) - (pC.X - pA.X) * (pB.Y - pA.Y));
                            if (area2 > 1e-3)
                            {
                                double rad = (da * db * dc) / (2.0 * area2);
                                if (rad < 5000) hRadius = Math.Round(rad, 1);
                            }
                        }
                    }
                    catch { }
                }
                item.HorizontalRadius = hRadius;

                // 5. Tính khoảng dạt bụng cong M
                if (hRadius > 0 && checkHorizontalClearance)
                {
                    item.ClearanceWidthM = CalculateHorizontalClearance(hRadius, item.RequiredSight);
                    if (item.ClearanceWidthM > 2.0)
                    {
                        item.Status = SightCheckStatus.Warning;
                        item.Detail = $"Bụng đường cong R={hRadius:F0}m cần dạt chướng ngại vật M={item.ClearanceWidthM:F2}m";
                    }
                }

                // 6. Điểm mắt tài xế & mục tiêu 3D
                Point3d eyeBase = ptCenter + normal2d * laneOffset;
                item.EyePoint = new Point3d(eyeBase.X, eyeBase.Y, ptCenter.Z + eyeH);

                double sTgt = Math.Min(s + item.RequiredSight, totalLength);
                Point3d ptTgtCenter = curve.GetPointAtDist(sTgt);
                Vector3d tgtTangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(sTgt));
                double tgtTLen = Math.Sqrt(tgtTangent.X * tgtTangent.X + tgtTangent.Y * tgtTangent.Y);
                Vector3d tgtNormal = normal2d;
                if (tgtTLen > 1e-6)
                {
                    tgtNormal = new Vector3d(-tgtTangent.Y / tgtTLen, tgtTangent.X / tgtTLen, 0);
                }

                Point3d tgtBase = ptTgtCenter + tgtNormal * laneOffset;
                item.TargetPoint = new Point3d(tgtBase.X, tgtBase.Y, ptTgtCenter.Z + targetH);

                // 7. Kiểm tra tia ngắm 3D xuyên qua Surface
                if (surface != null && check3DSurface)
                {
                    Point3d? obstruction;
                    bool isClear = Check3DSightRay(surface, item.EyePoint, item.TargetPoint, out obstruction);
                    if (!isClear && obstruction.HasValue)
                    {
                        item.Status = SightCheckStatus.Fail;
                        item.ObstructionPoint = obstruction;

                        double distToObs = item.EyePoint.DistanceTo(obstruction.Value);
                        item.ActualSight = Math.Round(distToObs, 1);
                        item.Detail = $"Tia ngắm bị chắn bởi địa hình tại cách {item.ActualSight:F1}m (Yêu cầu: {item.RequiredSight:F1}m)";
                    }
                }

                if (string.IsNullOrEmpty(item.Detail))
                {
                    item.Detail = (item.Status == SightCheckStatus.Pass) ? "Đảm bảo tầm nhìn xe chạy an toàn" : "Cần lưu ý bố trí mặt cắt";
                }

                results.Add(item);
                if (s >= totalLength) break;
            }

            return results;
        }

        /// <summary>
        /// Xuất báo cáo kiểm tra tầm nhìn ra file Excel định dạng chuẩn
        /// </summary>
        public static void ExportToExcel(List<SightCheckItem> items, string filePath, string alignName, string standardName, int speed, string sightTypeName)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Báo cáo Tầm Nhìn");

                // Tiêu đề báo cáo
                ws.Cell(1, 1).Value = "BÁO CÁO KIỂM TRA & ĐÁNH GIÁ TẦM NHÌN XE CHẠY (SIGHT DISTANCE ANALYSIS)";
                ws.Range(1, 1, 1, 10).Merge().Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(15)
                    .Font.SetFontColor(XLColor.Navy)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                // Thông tin chung
                ws.Cell(3, 1).Value = "Tuyến đường:";
                ws.Cell(3, 2).Value = alignName;
                ws.Cell(3, 4).Value = "Tiêu chuẩn áp dụng:";
                ws.Cell(3, 5).Value = standardName;

                ws.Cell(4, 1).Value = "Vận tốc thiết kế:";
                ws.Cell(4, 2).Value = $"{speed} km/h";
                ws.Cell(4, 4).Value = "Loại tầm nhìn:";
                ws.Cell(4, 5).Value = sightTypeName;

                ws.Cell(5, 1).Value = "Ngày lập:";
                ws.Cell(5, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(5, 4).Value = "Tổng số vị trí kiểm tra:";
                ws.Cell(5, 5).Value = items.Count;

                int passCount = items.Count(i => i.Status == SightCheckStatus.Pass);
                int warnCount = items.Count(i => i.Status == SightCheckStatus.Warning);
                int failCount = items.Count(i => i.Status == SightCheckStatus.Fail);

                ws.Cell(6, 1).Value = "Kết quả tổng hợp:";
                ws.Cell(6, 2).Value = $"Đạt: {passCount} | Cảnh báo: {warnCount} | Không đạt: {failCount}";
                ws.Range(3, 1, 6, 5).Style.Font.SetFontSize(10);

                // Header bảng dữ liệu
                string[] headers = {
                    "STT", "Lý trình", "Tọa độ X", "Tọa độ Y", "Độ dốc dọc i (%)",
                    "Bán kính R (m)", "Tầm nhìn yc S_yc (m)", "Tầm nhìn thực S_tt (m)",
                    "Dạt bụng cong M (m)", "Đánh giá", "Chi tiết / Kiến nghị"
                };

                int rowStart = 8;
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(rowStart, c + 1);
                    cell.Value = headers[c];
                    cell.Style
                        .Font.SetBold(true)
                        .Font.SetFontColor(XLColor.White)
                        .Fill.SetBackgroundColor(XLColor.SteelBlue)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                }

                // Điền dữ liệu
                int currentRow = rowStart + 1;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    ws.Cell(currentRow, 1).Value = i + 1;
                    ws.Cell(currentRow, 2).Value = item.StationFormatted;
                    ws.Cell(currentRow, 3).Value = Math.Round(item.X, 3);
                    ws.Cell(currentRow, 4).Value = Math.Round(item.Y, 3);
                    ws.Cell(currentRow, 5).Value = item.GradePercent;

                    string rStr = item.HorizontalRadius > 0 ? $"R_nam={item.HorizontalRadius:F0}" : (item.VerticalRadius > 0 ? $"R_dung={item.VerticalRadius:F0}" : "-");
                    ws.Cell(currentRow, 6).Value = rStr;
                    ws.Cell(currentRow, 7).Value = item.RequiredSight;
                    ws.Cell(currentRow, 8).Value = item.ActualSight;
                    ws.Cell(currentRow, 9).Value = item.ClearanceWidthM > 0 ? item.ClearanceWidthM : 0;

                    var statusCell = ws.Cell(currentRow, 10);
                    if (item.Status == SightCheckStatus.Pass)
                    {
                        statusCell.Value = "Đạt";
                        statusCell.Style.Font.SetFontColor(XLColor.DarkGreen).Font.SetBold(true);
                    }
                    else if (item.Status == SightCheckStatus.Warning)
                    {
                        statusCell.Value = "Cảnh báo";
                        statusCell.Style.Font.SetFontColor(XLColor.DarkOrange).Font.SetBold(true);
                    }
                    else
                    {
                        statusCell.Value = "Không đạt";
                        statusCell.Style.Font.SetFontColor(XLColor.Crimson).Font.SetBold(true);
                    }

                    ws.Cell(currentRow, 11).Value = item.Detail;

                    currentRow++;
                }

                // Kẻ viền bảng & căn chỉnh cột
                var dataRange = ws.Range(rowStart, 1, currentRow - 1, headers.Length);
                dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
                ws.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }
    }

    #endregion

    #region Command Class

    public class CTA_KiemTraTamNhin_Cmd
    {
        [CommandMethod("CTA_Kiem_Tra_Tam_Nhin")]
        public static void KiemTraTamNhin()
        {
            try
            {
                var form = new MyFirstProject.Civil_Tool.KiemTraTamNhinForm();
                Application.ShowModalDialog(form);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi lệnh CTA_KiemTraTamNhin: {ex.Message}");
            }
        }
    }

    #endregion
}

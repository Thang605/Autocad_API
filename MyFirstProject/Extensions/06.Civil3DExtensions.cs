using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Linq;

namespace Civil3DCsharp
{
    /// <summary>
    /// Extension methods for Civil 3D objects
    /// </summary>
    public static class Civil3DExtensions
    {
        /// <summary>
        /// Gets the parent alignment ID for a sample line
        /// </summary>
        /// <param name="sampleLine">The sample line</param>
        /// <returns>ObjectId of the parent alignment</returns>
        public static ObjectId GetParentAlignmentId(this SampleLine sampleLine)
        {
            ArgumentNullException.ThrowIfNull(sampleLine);
            
            ObjectId groupId = sampleLine.GroupId;
            if (groupId.IsNull || !groupId.IsValid || groupId.IsErased)
                return ObjectId.Null;

            // Đi thẳng qua nhóm cọc, không quét mọi tuyến trong bản vẽ đang active.
            // Dùng database của cọc để hỗ trợ cả đối tượng từ bản vẽ khác.
            Transaction? activeTr = sampleLine.Database.TransactionManager.TopTransaction;
            if (activeTr != null)
            {
                var activeGroup = activeTr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                return activeGroup?.ParentAlignmentId ?? ObjectId.Null;
            }

            using var tr = sampleLine.Database.TransactionManager.StartOpenCloseTransaction();
            var group = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
            ObjectId alignmentId = group?.ParentAlignmentId ?? ObjectId.Null;
            tr.Commit();
            return alignmentId;
        }

        /// <summary>
        /// Gets the section view group object for a section view
        /// </summary>
        /// <param name="sectionView">The section view</param>
        /// <returns>The section view group object</returns>
        public static SectionViewGroup? GetSectionViewGroupObject(this SectionView sectionView)
        {
            var db = sectionView.Database;
            using var tr = db.TransactionManager.StartTransaction();
            
            ObjectId sampleLineId = sectionView.SampleLineId;
            var sampleLineGroup = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLineGroup;
            if (sampleLineGroup == null) return null;
            
            foreach (SectionViewGroup sectionViewGroup in sampleLineGroup.SectionViewGroups)
            {
                if (sectionViewGroup.GetSectionViewIds().Contains(sectionView.ObjectId))
                {
                    return sectionViewGroup;
                }
            }
            return null;
        }

        /// <summary>
        /// Legacy extension method for backward compatibility
        /// </summary>
        /// <param name="sl">The sample line</param>
        /// <returns>ObjectId of the parent alignment</returns>
        public static ObjectId ParentAlignmentId(this SampleLine sl)
        {
            return sl.GetParentAlignmentId();
        }

        /// <summary>
        /// Legacy extension method for backward compatibility
        /// </summary>
        /// <param name="sv">The section view</param>
        /// <returns>The section view group object</returns>
        public static SectionViewGroup SectionViewGroupObject(this SectionView sv)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return sv.GetSectionViewGroupObject();
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}

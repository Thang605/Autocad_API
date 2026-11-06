## 2024-05-14 - Modal Dialog Keyboard Accessibility
**Learning:** Found multiple Windows Forms dialogs (e.g., DoiTenParcelForm, XoaRegionBiTatForm) missing standard AcceptButton/CancelButton assignments, preventing users from quickly using Enter/Escape to submit or close forms.
**Action:** Always ensure that this.AcceptButton = btnOK and this.CancelButton = btnCancel are defined in InitializeComponent() for all modal Form classes.

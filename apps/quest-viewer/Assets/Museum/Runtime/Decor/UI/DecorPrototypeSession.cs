using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FamilyMuseum.Decor
{
    public sealed class DecorPrototypeSession : MonoBehaviour
    {
        public DecorPrototypePersistence Persistence { get; private set; }
        public string StoreRoot { get; private set; }
        public bool Editing { get; private set; }
        public bool Modal { get; private set; }
        public bool CanEdit { get { return Editing && !Modal && Persistence.CanEdit; } }
        DecorPanel panel;
        bool confirmingCancel;
        // Explicit diagnostic hooks used by the real Player failure tests, never enabled by normal UI.
        internal bool FailPrepareOnce, FailActivateOnce;
        public void Open(DecorPanel value)
        {
            panel = value; string[] args = Environment.GetCommandLineArgs(); int smoke = Array.IndexOf(args, "-decor-smoke");
            StoreRoot = smoke >= 0 && smoke + 1 < args.Length ? Path.Combine(args[smoke + 1], "store") : Path.Combine(Application.persistentDataPath, "DecorPrototype-v1");
            int probe = Array.IndexOf(args, "-decor-probe");
            if (probe >= 0 && probe + 2 < args.Length) StoreRoot = args[probe + 1];
            Persistence = new DecorPrototypePersistence(StoreRoot, panel.Document.catalog, panel.Document.world);
            if (Persistence.Baseline != null) panel.Document.world = Persistence.Baseline;
            Editing = Persistence.CanEdit;
        }
        public void BindUI()
        {
            panel.Root.Q<Button>("apply").clicked += Apply;
            panel.Root.Q<Button>("cancel-edit").clicked += RequestCancel;
            panel.Root.Q<Button>("edit-again").clicked += BeginEdit;
            panel.Root.Q<Button>("reload-saved").clicked += Reload;
            panel.Root.Q<Button>("discard-confirm").clicked += ConfirmCancel;
            panel.Root.Q<Button>("keep-editing").clicked += () => { Modal = confirmingCancel = false; Refresh(); };
            Refresh();
            if (Persistence.State == "readOnly") panel.SetStatus(Persistence.Diagnostic == "WORLD_IN_USE" ? "다른 창에서 편집 중입니다. 읽기 전용으로 열었습니다. 편집 창을 닫고 다시 실행해 주세요." : "쓰기 권한을 얻지 못해 읽기 전용으로 열었습니다. 저장 위치와 권한을 확인한 뒤 다시 실행해 주세요.");
            else if (Persistence.State != "ready") Problem("저장을 열 수 없습니다. 손상되거나 다른 버전의 파일을 자동으로 덮어쓰지 않습니다. 확인 후 다시 불러오세요.");
            else panel.SetStatus("꾸미기 시험실 · 적용하면 배치를 저장합니다. 전체 취소는 마지막 적용 상태로 돌아갑니다.");
        }
        public void BeginEdit()
        {
            if (!Persistence.CanEdit || Modal) return;
            panel.Editor.Accept(Persistence.Baseline); Editing = true; Refresh(); panel.SetStatus("꾸미기를 시작했습니다. 적용 전까지 저장된 배치는 유지됩니다.");
        }
        public void Apply()
        {
            if (!CanEdit) return;
            if (panel.Editor.Interaction.HasPreview) { panel.SetStatus("미리보기를 먼저 놓거나 취소한 뒤 적용해 주세요."); return; }
            var result = Persistence.Apply(panel.Editor.Interaction.Snapshot(), Prepare);
            Debug.Log("Decor Apply: " + result.status + " / " + result.diagnosticCode);
            if (result.status == "activated")
            { Editing = false; panel.Editor.Accept(Persistence.Baseline); Refresh(); panel.SetStatus("배치를 저장했습니다. 시험 공간 둘러보기 · 다시 꾸미기로 편집할 수 있습니다."); }
            else if (result.saved) Problem("배치는 저장됐지만 화면에 반영하지 못했습니다. 저장된 배치를 다시 불러오세요.");
            else { panel.SetStatus(result.status == "prepareFailed" ? "화면을 준비하지 못해 저장하지 않았습니다. 편집 내용을 유지했습니다." : "저장하지 못했습니다. 편집 내용을 유지했습니다. 저장 위치와 권한을 확인한 뒤 다시 시도하세요."); Refresh(); }
        }
        IDecorPreparedView Prepare(DecorWorld candidate)
        {
            if (FailPrepareOnce) { FailPrepareOnce = false; throw new InvalidOperationException("Injected prepare failure"); }
            return new GuardedView(this, panel.PrepareWorld(candidate));
        }
        sealed class GuardedView : IDecorPreparedView
        {
            readonly DecorPrototypeSession owner; readonly IDecorPreparedView view;
            internal GuardedView(DecorPrototypeSession owner, IDecorPreparedView view) { this.owner = owner; this.view = view; }
            public void Activate(DecorWorld world)
            { if (owner.FailActivateOnce) { owner.FailActivateOnce = false; throw new InvalidOperationException("Injected activation failure"); } view.Activate(world); }
            public void Dispose() { view.Dispose(); }
        }
        public void RequestCancel()
        {
            if (!CanEdit) return;
            bool changed = DecorJson.WriteWorld(panel.Editor.Interaction.Snapshot()) != DecorJson.WriteWorld(Persistence.Baseline);
            if (!changed) { ConfirmCancel(); return; }
            panel.Editor.Cancel(); Modal = confirmingCancel = true;
            panel.Root.Q<Label>("session-message").text = "적용하지 않은 변경을 버리고 마지막 적용 상태로 돌아갈까요?"; Refresh();
        }
        public void ConfirmCancel()
        {
            if (!Persistence.CanEdit) return;
            panel.Editor.Reset(Persistence.Cancel()); Editing = false; Modal = confirmingCancel = false; Refresh(); panel.SetStatus("변경을 취소했습니다. 저장된 배치는 바뀌지 않았습니다.");
        }
        public void Reload()
        {
            if (confirmingCancel) return;
            var result = Persistence.Reload(Prepare);
            if (result.status == "reloaded") { panel.Editor.Accept(Persistence.Baseline); Editing = false; Modal = false; panel.ShowWorld(true); Refresh(); panel.SetStatus("저장된 배치를 불러왔습니다."); }
            else Problem("저장된 배치를 불러오지 못했습니다. 파일을 덮어쓰지 않았습니다. 저장 상태를 확인한 뒤 다시 시도하세요.");
        }
        void Problem(string message)
        { Editing = false; Modal = true; confirmingCancel = false; panel.Editor?.Cancel(); panel.ShowWorld(false); panel.Root.Q<Label>("session-message").text = message; panel.SetStatus(message); Refresh(); }
        public void Refresh()
        {
            if (panel.Root == null || panel.Editor == null) return;
            panel.Root.Q("session-overlay").style.display = Modal ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Root.Q("discard-confirm").style.display = confirmingCancel ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Root.Q("keep-editing").style.display = confirmingCancel ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Root.Q("reload-saved").style.display = Modal && !confirmingCancel ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Root.Q<Button>("apply").SetEnabled(CanEdit); panel.Root.Q<Button>("cancel-edit").SetEnabled(CanEdit);
            panel.Root.Q("edit-again").style.display = !Editing && !Modal && Persistence.CanEdit ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Root.Q("catalog").SetEnabled(CanEdit); panel.Root.Q("details").SetEnabled(!Modal);
            panel.RefreshTools();
        }
        void OnDestroy() { Persistence?.Dispose(); }
    }
}

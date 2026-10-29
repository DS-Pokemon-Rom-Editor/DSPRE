using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;
using VM = DSPRE.Avalonia.ViewModels.Pokemon.FollowerTalkEditorViewModel;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class FollowerTalkEditorView : UserControl
    {
        private VM Model => DataContext as VM;

        public FollowerTalkEditorView() { InitializeComponent(); }

        public FollowerTalkEditorView(VM vm) : this()
        {
            DataContext = vm;
            // Made here because its timer runs on the UI thread, and the editor's model is read behind the opening card.
            FollowerTalkPreviewViewModel preview = new FollowerTalkPreviewViewModel(vm);
            PreviewPane.DataContext = preview;
            DetachedFromVisualTree += (_, _) => { preview.Detach(); vm.Detach(); };
        }

        private static T Item<T>(object sender) where T : class => (sender as Control)?.DataContext as T;

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try { if (Model != null) await Model.SaveChangesAsync(); }
            catch (System.Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync("Follower talk wasn't saved:\n" + ex.Message, "Follower talk");
            }
        }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) Model?.DiscardChanges();
        }

        private void SlotUp_Click(object sender, RoutedEventArgs e) => Model?.MoveSlot(-1);
        private void SlotDown_Click(object sender, RoutedEventArgs e) => Model?.MoveSlot(1);
        private void SlotCopy_Click(object sender, RoutedEventArgs e) => Model?.CopySlot();
        private void SlotPaste_Click(object sender, RoutedEventArgs e) => Model?.PasteSlot();
        private void SlotClear_Click(object sender, RoutedEventArgs e) => Model?.ClearSlot();
        private void SlotFill_Click(object sender, RoutedEventArgs e) => Model?.FillSlot();
        private void OpenReaction_Click(object sender, RoutedEventArgs e)
        {
            if (Model?.SelectedSlot?.Data is { } slot) Model.ShowReaction(slot.Reaction);
        }

        private void StepUp_Click(object sender, RoutedEventArgs e) => Item<VM.StepVM>(sender)?.MoveUp();
        private void StepDown_Click(object sender, RoutedEventArgs e) => Item<VM.StepVM>(sender)?.MoveDown();
        private void StepRemove_Click(object sender, RoutedEventArgs e) => Item<VM.StepVM>(sender)?.Remove();
        private void StepNewLine_Click(object sender, RoutedEventArgs e) => Item<VM.StepVM>(sender)?.NewLine();
        private void StepAdd_Click(object sender, RoutedEventArgs e) => Model?.ReactionEditor?.AddStep();
        private void OpenYes_Click(object sender, RoutedEventArgs e) { if (Model?.ReactionEditor is { } r) Model.ShowReaction((int)r.Yes); }
        private void OpenNo_Click(object sender, RoutedEventArgs e) { if (Model?.ReactionEditor is { } r) Model.ShowReaction((int)r.No); }

        private void NewMove_Click(object sender, RoutedEventArgs e) => Model?.NewMove();
        private void FrameAdd_Click(object sender, RoutedEventArgs e) => Model?.MoveEditor?.AddFrame();
        private void FrameRemove_Click(object sender, RoutedEventArgs e) => Item<VM.FrameVM>(sender)?.Remove();
    }
}

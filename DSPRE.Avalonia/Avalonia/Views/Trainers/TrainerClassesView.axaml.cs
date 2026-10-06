using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class TrainerClassesView : UserControl
    {
        private TrainerClassesViewModel VM => DataContext as TrainerClassesViewModel;

        public TrainerClassesView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => VM?.ClassList.KeepInView(ClassList);
        }

        public TrainerClassesView(TrainerClassesViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private void TogglePlay_Click(object sender, RoutedEventArgs e) => VM?.TogglePlay();

        // Adds the entry with music = 0/0 so the Main/Alt fields can be set before Save writes it.
        private async void EnableMusic_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (VM.MusicNeedsExpansion && !await Arm9ExpansionOffer.EnsureAsync("Eye-contact music for this class", "Trainer Classes")) return;
            VM.EnableMusic(0, 0);
        }

        private void EditSprite_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || VM.SelectedClassIndex < 0 || !VM.CanEditSprite) return;
            TrainerClassesViewModel classesVm = VM;
            AvaloniaEditorLauncher.OpenTrainerSpriteEditor(VM.SelectedClassIndex, () => classesVm.RefreshSpritePreview());
        }

        private async void PatchNotes_Click(object sender, RoutedEventArgs e)
        {
            DSPRE.PatchToolboxLogic.PatchInfo patch = new()
            {
                Key = "trainerClassMetadata", Title = "Trainer class metadata", State = DSPRE.PatchToolboxLogic.PatchState.Applied,
            };
            await DialogHelper.ShowInfo(DSPRE.PatchNotes.For(patch), patch.Title);
        }

        private void EditIntro_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && VM.SelectedClassIndex >= 0) AvaloniaEditorLauncher.OpenVsIntroEditor(VM.SelectedClassIndex);
        }

        private async void CopyClass_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || VM.SelectedClassIndex < 0) return;
            if (!await VM.ConfirmLeaveAsync()) return;
            string error = VM.CopySelectedClass();
            if (error != null) await DialogHelper.ShowError(error, "Copy Class");
        }

        private async void RemoveLastClass_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || VM.LastClassIndex < 1) return;
            if (!await VM.ConfirmLeaveAsync()) return;
            int last = VM.LastClassIndex;
            string error = null;
            List<DSPRE.ROMFiles.TrainerReference> uses = null;
            TrainerClassesViewModel vm = VM;
            await BusyOverlay.RunAsync($"Checking class {last}",
                "Looking for trainers, events, scripts, rematches and Battle Tower trainers that use it.",
                () => uses = vm.LastClassUses(out error), TopLevel.GetTopLevel(this) as Window);
            if (uses == null) { await DialogHelper.ShowError(error, "Remove Last Class"); return; }
            if (uses.Count > 0)
            {
                await DialogHelper.ShowError($"Class {last} can't be removed while these use it:\n\n" +
                    DSPRE.ROMFiles.TrainerClassDatasetManager.DescribeUses(uses), "Remove Last Class");
                return;
            }
            bool sure = await DialogHelper.AskYesNo(
                $"Remove class {last}? No trainer, event, script, rematch, phone contact or Battle Tower trainer uses it. " +
                "Custom code isn't checked.", "Remove Last Class");
            if (!sure) return;
            error = VM.RemoveLastClass();
            if (error != null) await DialogHelper.ShowError(error, "Remove Last Class");
        }

        private async void AddTrainerClass_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (!VM.CanAddClass)
            {
                await DialogHelper.ShowError("Save or discard the new trainer class first.", "Add Trainer Class");
                return;
            }
            // The new class is selected once it is added, so the loaded class is settled first.
            if (!await VM.ConfirmLeaveAsync()) return;
            bool hgEngine = DSPRE.HgEngine.HgEngineProject.IsActive;
            if (!hgEngine && !await Arm9ExpansionOffer.EnsureAsync("A new trainer class", "Add Trainer Class")) return;

            int newClassId = VM.NextClassId;
            string refusal = hgEngine ? DSPRE.HgEngine.HgEngineTrainerClassExpansion.AddRefusal() : null;
            if (refusal != null) { await DialogHelper.ShowError(refusal, "Add Trainer Class"); return; }

            AddTrainerClassViewModel dlgVm = new AddTrainerClassViewModel { ForHgEngine = hgEngine };
            dlgVm.SetSpriteChoices(VM.ClassNames, VM.SelectedClassIndex, newClassId, VM.NextClassHasSprite(newClassId));
            AddTrainerClassView dlg = new AddTrainerClassView(dlgVm);
            Window owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();

            if (!dlgVm.Confirmed) return;

            string error = VM.AddTrainerClass(dlgVm.ClassName, dlgVm.NameWithArticle, (byte)dlgVm.GenderIndex, (byte)dlgVm.PrizeMultiplier,
                dlgVm.AddMusic, (ushort)dlgVm.MusicMain, 0, dlgVm.SpriteFrom);
            if (error != null)
                await DialogHelper.ShowError(error, "Add Trainer Class");
        }
    }
}

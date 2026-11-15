using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class HeadbuttEncounterViewModel
    {
        private void RefreshGroups()
        {
            NormalTreeGroups.Clear();
            for (int i = 0; i < _file.NormalTreeGroups.Count; i++) NormalTreeGroups.Add($"Group {i + 1}");
            SpecialTreeGroups.Clear();
            for (int i = 0; i < _file.SpecialTreeGroups.Count; i++) SpecialTreeGroups.Add($"Group {i + 1}");
            SelectedGroupTrees.Clear();
        }

        public bool CanAddTree => CurrentGroup()?.Trees.Any(t => t.IsUnused) == true;
        public string AddTreeHelp => CanAddTree ? "Add a tree" : "All tree slots in this group are in use";

        private void ShowGroupTrees()
        {
            OnPropertyChanged(nameof(CanAddTree));
            OnPropertyChanged(nameof(AddTreeHelp));
            SelectedGroupTrees.Clear();
            if (_file == null || _selGroup < 0) return;
            BindingList<HeadbuttTreeGroup> groups = _specialGroupActive ? _file.SpecialTreeGroups : _file.NormalTreeGroups;
            if (_selGroup >= groups.Count) return;
            List<HeadbuttTree> trees = groups[_selGroup].Trees;
            // Only used slots show, since empty ones read 65535, 65535; the name keeps the slot number.
            for (int i = 0; i < trees.Count; i++)
                if (!trees[i].IsUnused)
                    SelectedGroupTrees.Add(new HeadbuttTreeRow($"Tree {i + 1}", trees[i], OnTreeChanged));
            _selTree = -1;
            OnPropertyChanged(nameof(SelectedNormalGroupIndex));
            OnPropertyChanged(nameof(SelectedSpecialGroupIndex));
            OnPropertyChanged(nameof(SelectedTreeIndex));
            OnPropertyChanged(nameof(GroupTreeSummary));
            RefreshTreeMarkers();   // the map is already built for the whole file, only re-mark this group
            FocusGroup();
        }

        /// <summary>Raised with the selected group's centre in normalized render space.</summary>
        public event EventHandler<(float X, float Y, float Z)> GroupFocused;

        private void FocusGroup()
        {
            if (_selGroup < 0 || Model3D == null) return;
            foreach (TreeBox box in _treeBoxes)
                if (box.Special == _specialGroupActive && box.Group == _selGroup)
                {
                    GroupFocused?.Invoke(this, ((box.MinX + box.MaxX) / 2, box.Ground, (box.MinZ + box.MaxZ) / 2));
                    return;
                }
        }

        public string GroupTreeSummary
        {
            get
            {
                if (_file == null || _selGroup < 0) return "";
                BindingList<HeadbuttTreeGroup> groups = _specialGroupActive ? _file.SpecialTreeGroups : _file.NormalTreeGroups;
                if (_selGroup >= groups.Count) return "";
                int used = 0, total = groups[_selGroup].Trees.Count;
                foreach (HeadbuttTree t in groups[_selGroup].Trees) if (!t.IsUnused) used++;
                return $"{used} of {total} trees";
            }
        }

        private HeadbuttTreeGroup CurrentGroup()
        {
            if (_file == null || _selGroup < 0) return null;
            BindingList<HeadbuttTreeGroup> groups = _specialGroupActive ? _file.SpecialTreeGroups : _file.NormalTreeGroups;
            return _selGroup < groups.Count ? groups[_selGroup] : null;
        }

        public void AddGroup(bool special)
        {
            if (_file == null) return;
            BindingList<HeadbuttTreeGroup> groups = special ? _file.SpecialTreeGroups : _file.NormalTreeGroups;
            groups.Add(new HeadbuttTreeGroup());
            Dirty();
            RefreshGroups();
            if (special) SelectedSpecialGroupIndex = groups.Count - 1; else SelectedNormalGroupIndex = groups.Count - 1;
        }

        /// <summary>Adds a copy of the selected group, trees and all, and selects it.</summary>
        public void DuplicateGroup(bool special)
        {
            if (_file == null || _specialGroupActive != special || CurrentGroup() is not HeadbuttTreeGroup group) return;
            BindingList<HeadbuttTreeGroup> groups = special ? _file.SpecialTreeGroups : _file.NormalTreeGroups;
            groups.Add(new HeadbuttTreeGroup(group));
            Dirty();
            RefreshGroups();
            if (special) SelectedSpecialGroupIndex = groups.Count - 1; else SelectedNormalGroupIndex = groups.Count - 1;
        }

        public void RemoveGroup(bool special)
        {
            if (_file == null || _specialGroupActive != special || _selGroup < 0) return;
            BindingList<HeadbuttTreeGroup> groups = special ? _file.SpecialTreeGroups : _file.NormalTreeGroups;
            if (_selGroup >= groups.Count) return;
            int keep = Math.Min(_selGroup, groups.Count - 2);
            groups.RemoveAt(_selGroup);
            Dirty();
            RefreshGroups();
            if (keep >= 0) { if (special) SelectedSpecialGroupIndex = keep; else SelectedNormalGroupIndex = keep; }
            else ShowGroupTrees();
        }

        /// <summary>Makes the group's first empty slot a tree, on another tree's cell or 0,0.</summary>
        public void AddTree()
        {
            HeadbuttTreeGroup g = CurrentGroup();
            if (g == null) return;
            HeadbuttTree slot = null;
            foreach (HeadbuttTree t in g.Trees) if (t.IsUnused) { slot = t; break; }
            if (slot == null) return;
            ushort gx = 0, gy = 0;
            foreach (HeadbuttTree t in g.Trees) if (!t.IsUnused) { gx = t.GlobalX; gy = t.GlobalY; break; }
            slot.GlobalX = gx; slot.GlobalY = gy;
            Dirty();
            ShowGroupTrees();
            SelectedTreeIndex = SelectedGroupTrees.Count - 1;
        }

        public void RemoveSelectedTree()
        {
            if (_selTree < 0 || _selTree >= SelectedGroupTrees.Count) return;
            HeadbuttTree t = SelectedGroupTrees[_selTree].Tree;
            t.GlobalX = ushort.MaxValue; t.GlobalY = ushort.MaxValue;
            Dirty();
            ShowGroupTrees();
        }
    }
}

﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Export;
using CompMs.CommonMVVM;
using CompMs.CommonMVVM.Validator;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.ViewModel.Export
{
    internal sealed class AnalysisResultExportViewModel : ViewModelBase
    {
        private readonly AnalysisResultExportModel _model;

        public AnalysisResultExportViewModel(AnalysisResultExportModel model) {
            _model = model ?? throw new ArgumentNullException(nameof(model));

            SelectedFrom = model.UnSelectedFiles.ToReadOnlyReactiveCollection(file => new FileBeanSelection(file)).AddTo(Disposables);
            SelectedTo = model.SelectedFiles.ToReadOnlyReactiveCollection(file => new FileBeanSelection(file)).AddTo(Disposables);

            ExportSpectraTypes = new ReadOnlyObservableCollection<SpectraType>(model.ExportSpectraTypes);
            ExportSpectraFileFormats = new ReadOnlyObservableCollection<SpectraFormat>(model.ExportSpectraFileFormats);

            model.ObserveProperty(m => m.SelectedSpectraType).Subscribe(t => SelectedSpectraType = t).AddTo(Disposables);
            model.ObserveProperty(m => m.SelectedFileFormat).Subscribe(f => SelectedFileFormat = f).AddTo(Disposables);
            model.ObserveProperty(m => m.IsotopeExportMax).Subscribe(m => IsotopeExportMax = m).AddTo(Disposables);

            ExportPeakCommand = this.ErrorsChangedAsObservable().ToUnit()
                .StartWith(Unit.Default)
                .Select(_ => !HasValidationErrors)
                .ToAsyncReactiveCommand()
                .WithSubscribe(ExportPeakAsync)
                .AddTo(Disposables);
        }

        public AsyncReactiveCommand ExportPeakCommand { get; }
        private Task ExportPeakAsync() => Task.Run(_model.Export);

        public ReadOnlyReactiveCollection<FileBeanSelection> SelectedFrom { get; }
        public ReadOnlyReactiveCollection<FileBeanSelection> SelectedTo { get; }

        public DelegateCommand AddItemsCommand => _addItemsCommand ?? (_addItemsCommand = new DelegateCommand(AddItems));
        private DelegateCommand _addItemsCommand;

        private void AddItems() {
            _model.Selects(SelectedFrom.Where(file => file.IsChecked).Select(file => file.File));
        }

        public DelegateCommand AddAllItemsCommand => _addAllItemsCommand ?? (_addAllItemsCommand = new DelegateCommand(AddAllItems));
        private DelegateCommand _addAllItemsCommand;

        private void AddAllItems() {
            _model.Selects(SelectedFrom.Select(file => file.File));
        }

        public DelegateCommand RemoveItemsCommand => _removeItemsCommand ?? (_removeItemsCommand = new DelegateCommand(RemoveItems));
        private DelegateCommand _removeItemsCommand;

        private void RemoveItems() {
            _model.UnSelects(SelectedTo.Where(file => file.IsChecked).Select(file => file.File));
        }

        public DelegateCommand RemoveAllItemsCommand => _removeAllItemsCommand ?? (_removeAllItemsCommand = new DelegateCommand(RemoveAllItems));
        private DelegateCommand _removeAllItemsCommand;

        private void RemoveAllItems() {
            _model.UnSelects(SelectedTo.Select(file => file.File));
        }

        public DelegateCommand SelectDestinationCommand => _selectDestinationCommand ?? (_selectDestinationCommand = new DelegateCommand(SelectDestination));
        private DelegateCommand _selectDestinationCommand;

        [Required(ErrorMessage = "Choose a folder for the exported files.")]
        [PathExists(ErrorMessage = "Choose an existing folder", IsDirectory = true)]
        public string DestinationFolder {
            get {
                return _destinationFolder;
            }
            set {
                if (SetProperty(ref _destinationFolder, value)) {
                    if (!ContainsError(nameof(DestinationFolder))) {
                        _model.DestinationFolder = _destinationFolder;
                    }
                }
            }
        }
        private string _destinationFolder;

        public void SelectDestination() {
            var fbd = new Graphics.Window.SelectFolderDialog
            {
                Title = "Chose a export folder.",
            };
            if (fbd.ShowDialog() == Graphics.Window.DialogResult.OK) {
                DestinationFolder = fbd.SelectedPath;
            }
        }

        public ReadOnlyObservableCollection<SpectraType> ExportSpectraTypes { get; }

        [Required(ErrorMessage = "Choose a spectra type.")]
        public SpectraType SelectedSpectraType {
            get {
                return _selectedSpectraType;
            }

            set {
                if (SetProperty(ref _selectedSpectraType, value)) {
                    if (!ContainsError(nameof(SelectedSpectraType))) {
                        _model.SelectedSpectraType = _selectedSpectraType;
                    }
                }
            }
        }
        private SpectraType _selectedSpectraType;

        public ReadOnlyObservableCollection<SpectraFormat> ExportSpectraFileFormats { get; }

        [Required(ErrorMessage = "Choose a spectra format.")]
        public SpectraFormat SelectedFileFormat {
            get {
                return _selectedFileFormat;
            }

            set {
                if (SetProperty(ref _selectedFileFormat, value)) {
                    if (!ContainsError(nameof(SelectedFileFormat))) {
                        _model.SelectedFileFormat = _selectedFileFormat;
                    }
                }
            }
        }
        private SpectraFormat _selectedFileFormat;

        public int IsotopeExportMax {
            get {
                return _isotopeExportMax;
            }
            set {
                if (SetProperty(ref _isotopeExportMax, value)) {
                    if (!ContainsError(nameof(IsotopeExportMax))) {
                        _model.IsotopeExportMax = _isotopeExportMax;
                    }
                }
            }
        }
        private int _isotopeExportMax = 2;
    }

    internal sealed class FileBeanSelection : ViewModelBase
    {
        public FileBeanSelection(AnalysisFileBeanModel file) {
            File = file;
        }

        public AnalysisFileBeanModel File { get; }

        public string FileName => File.AnalysisFileName;

        public bool IsChecked {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
        private bool _isChecked;
    }
}

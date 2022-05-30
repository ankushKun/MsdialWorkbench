﻿using CompMs.App.Msdial.Common;
using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.CommonMVVM;
using CompMs.Graphics.AxisManager;
using CompMs.Graphics.Base;
using CompMs.Graphics.Core.Base;
using CompMs.Graphics.Design;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Media;

namespace CompMs.App.Msdial.ViewModel.Chart
{
    class BarChartViewModel : ViewModelBase
    {
        public BarChartViewModel(BarChartModel model, IAxisManager<string> horizontalAxis = null, IAxisManager<double> verticalAxis = null) {

            if (model is null) {
                throw new ArgumentNullException(nameof(model));
            }
            this.model = model;

            BarItems = this.model.BarItemsSource
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            if (horizontalAxis is null) {
                horizontalAxis = this.model.BarItemsSource
                    .Select(items => items.Select(item => item.Class).ToArray())
                    .ToReactiveCategoryAxisManager()
                    .AddTo(Disposables);
            }
            HorizontalAxis = horizontalAxis;

            if (verticalAxis is null) {
                verticalAxis = this.model.VerticalRangeAsObservable
                    .ToReactiveAxisManager<double>(new RelativeMargin(0, 0.05), new Range(0, 0), LabelType.Order)
                    .AddTo(Disposables);
            }
            VerticalAxis = verticalAxis;

            var brushSource = model.ClassBrush;
            if (brushSource is null) {
                brushSource = BarItems.Select(
                    items => new KeyBrushMapper<BarItem>(
                        items.Zip(ChartBrushes.SolidColorBrushList, (item, brush) => (item, brush))
                            .ToDictionary(p => p.item, p => (Brush)p.brush),
                        Brushes.Blue
                    ));
            }
            BrushSource = brushSource.ToReadOnlyReactivePropertySlim().AddTo(Disposables);

            Errors = BarItems
                .Where(items => items != null)
                .Select(items => items.Select(item => item.Error).ToArray())
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            HorizontalTitle = this.model.Elements
                .ObserveProperty(m => m.HorizontalTitle)
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            VerticalTitle = this.model.Elements
                .ObserveProperty(m => m.VerticalTitle)
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            HorizontalProperty = this.model.Elements
                .ObserveProperty(m => m.HorizontalProperty)
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            VerticalProperty = this.model.Elements
                .ObserveProperty(m => m.VerticalProperty)
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
        }

        private readonly BarChartModel model;

        public ReadOnlyReactivePropertySlim<List<BarItem>> BarItems { get; }

        public ReadOnlyReactivePropertySlim<double[]> Errors { get; }

        public IAxisManager<string> HorizontalAxis { get; }

        public IAxisManager<double> VerticalAxis { get; }

        public ReadOnlyReactivePropertySlim<string> HorizontalTitle { get; }

        public ReadOnlyReactivePropertySlim<string> VerticalTitle { get; }

        public ReadOnlyReactivePropertySlim<string> HorizontalProperty { get; }

        public ReadOnlyReactivePropertySlim<string> VerticalProperty { get; }

        public ReadOnlyReactivePropertySlim<IBrushMapper<BarItem>> BrushSource { get; }
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkyrimRpEncyclopedia.Models;
using SkyrimRpEncyclopedia.Services;

namespace SkyrimRpEncyclopedia.Views;

public partial class GraphView
{
    private readonly IGraphMapReader? _mapReader;
    private readonly GraphMapLayer _mapLayer = new() { Visibility = Visibility.Collapsed };
    private readonly GraphCamera _explorerCamera = new(), _mapCamera = new();
    private GraphMapDrawing? _fullDrawing;
    private bool _fullMode;
    private GraphEntityKey? _explorerSelection;
    private Point? _panPoint;
    internal GraphCamera Camera => _fullMode ? _mapCamera : _explorerCamera;
    internal GraphMapDrawing? FullDrawing => _fullDrawing;
    private Size ViewportSize => new(GraphViewport.ActualWidth, GraphViewport.ActualHeight);
    private IEnumerable<GraphEdge> LoadedEdges(GraphEntityKey key) => _fullMode && _fullDrawing is not null
        ? _fullDrawing.Adjacency.GetValueOrDefault(key, []) : Explorer.Edges.Values.Where(e => e.Source == key || e.Target == key);

    private async void Mode_Changed(object sender, SelectionChangedEventArgs e)
    { if (_ready) await SetFullModeAsync(ModeBox.SelectedIndex == 1); }
    internal async Task SetFullModeAsync(bool full)
    {
        CancelRequests();
        if (!_fullMode) _explorerSelection = _selectedKey;
        _fullMode = full; SearchResults.ItemsSource = null; SetSelectedArticle(null); _selectedKey = null;
        // No automatic cross-store full merge; preserve the exploration checkbox values.
        ShowOfficialCheckBox.IsEnabled = ShowPersonalCheckBox.IsEnabled = !full;
        GraphCanvas.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
        _mapLayer.Visibility = full ? Visibility.Visible : Visibility.Collapsed;
        if (full)
        {
            ExpandButton.IsEnabled = CenterSelectedButton.IsEnabled = false;
            if (_fullDrawing is null) await LoadFullMapAsync(); else await ApplyFullFiltersAsync();
        }
        else
        {
            _selectedKey = _explorerSelection; RenderGraph();
            if (Explorer.Nodes.Count == 0) ShowState(AtlasPresentationState.Initial);
            StatusText.Text = "Исследование восстановлено; координаты, фильтры и камера сохранены.";
        }
        ApplyCamera();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelRequests();
        if (_fullMode && _fullDrawing is null) ShowState(AtlasPresentationState.Initial);
        StatusText.Text = "Загрузка отменена. Уже открытая карта сохранена. Отмена SQLite кооперативная.";
    }

    internal async Task LoadFullMapAsync()
    {
        CancelRequests(); var epoch = _version;
        if (_mapReader is null) { ShowState(AtlasPresentationState.Error, "Knowledge DB не подключена к полной карте."); return; }
        var cts = new CancellationTokenSource(); _request = cts;
        var watch = Stopwatch.StartNew(); CancelButton.IsEnabled = true;
        GraphViewport.Visibility = _fullDrawing is null ? Visibility.Collapsed : Visibility.Visible;
        StateScroller.Visibility = _fullDrawing is null ? Visibility.Visible : Visibility.Collapsed;
        ShowState(AtlasPresentationState.Loading);
        IProgress<GraphMapProgress> progress = new Progress<GraphMapProgress>(p =>
        {
            if (!_active || !_fullMode || epoch != _version || cts.IsCancellationRequested) return;
            StatusText.Text = $"Knowledge DB · {p.Phase}: {p.Nodes:N0}/{p.TotalNodes:N0} сущностей · {p.Edges:N0}/{p.TotalEdges:N0} связей";
            CountText.Text = $"Читается: {p.Nodes:N0} / {p.Edges:N0}";
        });
        try
        {
            await _readSlot.WaitAsync(cts.Token);
            GraphMapDrawing drawing;
            try { drawing = await Task.Run(() =>
                {
                    var snapshot=_mapReader.Read(cts.Token, progress);
                    progress.Report(new(snapshot.Nodes.Count,snapshot.Edges.Count,snapshot.ExpectedNodes,snapshot.ExpectedEdges,"Подготовка карты"));
                    return GraphMapLayout.Prepare(snapshot,cts.Token);
                }, cts.Token); }
            finally { _readSlot.Release(); }
            if (!_active || !_fullMode || epoch != _version || cts.IsCancellationRequested) return;
            var first = _fullDrawing is null; _fullDrawing = drawing;
            var applying=ApplyFullFiltersAsync(); var applyEpoch=_version; await applying;
            if (!_active || !_fullMode || _fullDrawing != drawing || applyEpoch!=_version) return;
            if (first)
            {
                // Visibility has just changed from Collapsed: ActualWidth/Height may still be zero.
                GraphViewport.UpdateLayout();
                Camera.Fit(drawing.Bounds, ViewportSize); ApplyCamera();
            }
            StatusText.Text = $"Полная карта Knowledge DB · чтение и подготовка: {watch.Elapsed.TotalMilliseconds:F0} мс · направление записи не означает причинность.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (_active && epoch == _version) ShowState(AtlasPresentationState.Error, error.Message); }
        finally
        {
            if (ReferenceEquals(_request, cts)) { _request = null; CancelButton.IsEnabled = false; StateProgress.Visibility = Visibility.Collapsed; }
            cts.Dispose();
        }
    }
    internal async Task ApplyFullFiltersAsync()
    {
        if (!_fullMode || _fullDrawing is null) return;
        CancelRequests(); var epoch = _version; var drawing = _fullDrawing;
        var keys = drawing.Snapshot.Nodes.Values.Where(n => Sources().Contains(n.Key.SourceKind) && TypeVisible(n.Type)).Select(n => n.Key).ToHashSet();
        var cts = new CancellationTokenSource(); _request = cts; CancelButton.IsEnabled = true;
        try
        {
            (System.Windows.Media.Imaging.BitmapSource, Geometry) geometry;
            if (keys.Count == drawing.Snapshot.Nodes.Count) geometry = (drawing.EdgeImage, drawing.NodeGeometry);
            else
            {
                // Serialize expensive preparations as well as reads: rapid filters cannot allocate many raster buffers.
                await _readSlot.WaitAsync(cts.Token);
                try { geometry = await Task.Run(() => GraphMapLayout.Geometry(drawing.Snapshot, drawing.Positions, keys, cts.Token), cts.Token); }
                finally { _readSlot.Release(); }
            }
            if (!_active || !_fullMode || epoch != _version || cts.IsCancellationRequested) return;
            _mapLayer.Drawing = drawing; _mapLayer.VisibleKeys = keys; _mapLayer.Edges = geometry.Item1; _mapLayer.Nodes = geometry.Item2;
            _mapLayer.Selected = _selectedKey;
            GraphViewport.Visibility = keys.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            StateScroller.Visibility = keys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if(keys.Count == 0) ShowState(AtlasPresentationState.Empty);
            CountText.Text = $"Knowledge: {drawing.Snapshot.Nodes.Count:N0} сущностей / {drawing.Snapshot.Edges.Count:N0} связей · видно {keys.Count:N0} узлов";
            LimitText.Text = "Полная карта · только Knowledge DB · без бюджета исследования · подписи с 65% · панорама средней кнопкой";
            OpenArticleButton.IsEnabled = _selectedKey is GraphEntityKey key && keys.Contains(key) && _selectedArticle is not null;
            ExpandButton.IsEnabled = CenterSelectedButton.IsEnabled = false;
            SearchFullMap(SearchBox.Text);
            ApplyCamera();
        }
        catch(OperationCanceledException) { }
        catch(Exception error) { if (_active && epoch == _version) ShowState(AtlasPresentationState.Error,error.Message); }
        finally { if(ReferenceEquals(_request,cts)) { _request=null; CancelButton.IsEnabled=false; } cts.Dispose(); }
    }
    private sealed record MapSearchEntry(GraphMapNode Node)
    { public override string ToString() => $"{Node.Title} · Knowledge · #{Node.Key.Id}"; }
    private void SearchFullMap(string query)
    {
        SearchResults.ItemsSource = _fullDrawing?.Snapshot.Nodes.Values.Where(n => _mapLayer.VisibleKeys.Contains(n.Key) &&
            !string.IsNullOrWhiteSpace(query) && n.Title.Contains(query.Trim(),StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Title,StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Key.Id).Take(20).Select(n => new MapSearchEntry(n)).ToArray();
        StatusText.Text = _fullDrawing is null ? "Дождитесь загрузки полной карты." : "Поиск в загруженной полной карте · первые 20 результатов; уточните название.";
    }
    internal async Task SelectMapNodeAsync(GraphEntityKey key)
    {
        if (_mapReader is null || !_fullMode || _fullDrawing is null || !_mapLayer.VisibleKeys.Contains(key)) return;
        CancelRequests(); var epoch = _version; var cts=new CancellationTokenSource(); _request=cts;
        // Clear the old payload immediately; a failed read must not open the previous article under a new selection.
        _selectedKey=key; SetSelectedArticle(null); _mapLayer.Selected=key; _mapLayer.InvalidateVisual();
        try
        {
            await _readSlot.WaitAsync(cts.Token); Article article;
            try { article=await Task.Run(()=>_mapReader.ReadArticle(key,cts.Token),cts.Token); }
            finally { _readSlot.Release(); }
            if (_active && _fullMode && epoch==_version && !cts.IsCancellationRequested) SetSelectedArticle(article);
        }
        catch(OperationCanceledException) { }
        catch(Exception error) { if(_active && epoch==_version) ShowState(AtlasPresentationState.Error,error.Message); }
        finally { if(ReferenceEquals(_request,cts)) _request=null; cts.Dispose(); }
    }
    internal void ApplyCamera()
    {
        if (!_ready) return;
        GraphCanvas.RenderTransform = new MatrixTransform(_explorerCamera.Matrix);
        _mapLayer.Camera = _mapCamera; _mapLayer.InvalidateVisual();
        ZoomText.Text = $"{Camera.Scale:P0}";
    }
    private Point ViewCenter => new(GraphViewport.ActualWidth/2,GraphViewport.ActualHeight/2);
    internal void ZoomAt(double scale, Point point) { Camera.Zoom(scale,point); ApplyCamera(); }
    private void ZoomIn_Click(object sender,RoutedEventArgs e) => ZoomAt(Camera.Scale*1.2,ViewCenter);
    private void ZoomOut_Click(object sender,RoutedEventArgs e) => ZoomAt(Camera.Scale/1.2,ViewCenter);
    private void ZoomReset_Click(object sender,RoutedEventArgs e) => ZoomAt(1,ViewCenter);
    private void Fit_Click(object sender,RoutedEventArgs e)
    {
        Rect bounds=Rect.Empty;
        if(_fullMode && _fullDrawing is not null)
        { foreach(var key in _mapLayer.VisibleKeys) bounds.Union(new Rect(_fullDrawing.Positions[key],new Size(1,1))); }
        else foreach(var node in Explorer.Nodes.Values.Where(Visible))
        { var p=Explorer.Positions[node.Key]; bounds.Union(new Rect(p.X,p.Y,160,64)); }
        Camera.Fit(bounds,ViewportSize); ApplyCamera();
    }
    private void Viewport_Wheel(object sender,MouseWheelEventArgs e)
    { ZoomAt(Camera.Scale*Math.Pow(1.2,e.Delta/120d),e.GetPosition(GraphViewport)); e.Handled=true; }
    private async void Viewport_Down(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=MouseButton.Left && e.ChangedButton!=MouseButton.Middle) return;
        var point=e.GetPosition(GraphViewport);
        if(_fullMode && e.ChangedButton==MouseButton.Left && _mapLayer.Hit(point) is GraphEntityKey key)
        { e.Handled=true; await SelectMapNodeAsync(key); return; }
        _panPoint=point; GraphViewport.CaptureMouse(); e.Handled=true;
    }
    private void Viewport_Move(object sender,MouseEventArgs e)
    { if(_panPoint is Point previous && GraphViewport.IsMouseCaptured) { var point=e.GetPosition(GraphViewport); Camera.Pan(point-previous); _panPoint=point; ApplyCamera(); e.Handled=true; } }
    private void Viewport_Up(object sender,MouseButtonEventArgs e)
    { if(_panPoint is null) return; _panPoint=null; GraphViewport.ReleaseMouseCapture(); e.Handled=true; }
    private void Viewport_LostCapture(object sender,MouseEventArgs e) => _panPoint=null;
    private void Viewport_Resize(object sender,SizeChangedEventArgs e)
    { if(_ready) { _explorerCamera.Resize(e.PreviousSize,e.NewSize); _mapCamera.Resize(e.PreviousSize,e.NewSize); ApplyCamera(); } }
}

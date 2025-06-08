using System.Collections;
using System.Diagnostics;
using TZFinder;
using Windows.Devices.Geolocation;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;

namespace TimeZoneVisualizer;

public sealed partial class MainPage : Page
{
    private sealed partial class GeopositionEnumerable(IEnumerable<BasicGeoposition> geopositions) : IEnumerable<BasicGeoposition>
    {
        public IEnumerator<BasicGeoposition> GetEnumerator() => geopositions.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private MapElementsLayer _timeZoneLayer;
    private Nullable<TimeZoneIndex> _currentIndex;

    public MainPage()
    {
        InitializeComponent();

        Map.Layers.Add(_timeZoneLayer = new MapElementsLayer());
    }

    private void MapControl_MapTapped(MapControl sender, MapInputEventArgs args)
    {
        float longitude = (float)args.Location.Position.Longitude;
        float latitude = (float)args.Location.Position.Latitude;

        Stopwatch watch = Stopwatch.StartNew();
        string timeZoneIds = string.Join(", ", Lookup.GetAllTimeZoneIds(longitude, latitude, out TimeZoneIndex index));
        watch.Stop();

        TimeZone.Text = $"{timeZoneIds} {watch.ElapsedTicks} ticks";

        if (_currentIndex is null || index != _currentIndex.Value)
        {
            _timeZoneLayer.MapElements.Clear();

            MapPolygon polygon = new();

            Lookup.Traverse(index, box =>
            {
                polygon.Paths.Add(new Geopath(new GeopositionEnumerable([
                    new(box.SouthWest.Latitude, box.SouthWest.Longitude, 0),
                    new(box.SouthWest.Latitude, box.NorthEast.Longitude, 0),
                    new(box.NorthEast.Latitude, box.NorthEast.Longitude, 0),
                    new(box.NorthEast.Latitude, box.SouthWest.Longitude, 0),
                        ])));
            });

            int hash = index.First ^ index.Second;
            polygon.FillColor = Color.FromArgb(0x80, (byte)(hash * 200 % 256), (byte)(hash * 700 % 256), (byte)(hash * 1100 % 256));
            polygon.StrokeColor = Color.FromArgb(0xc0, 0, 0, 0);
            polygon.StrokeThickness = 1;

            _timeZoneLayer.MapElements.Add(polygon);

            _currentIndex = index;
        }
    }
}

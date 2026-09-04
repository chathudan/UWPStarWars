using System;
using System.IO;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DrawboardCodingExercise.Controls;

public sealed partial class StreamedImage
{
	public static readonly DependencyProperty SourceStreamProperty = DependencyProperty.Register(
		nameof(SourceStream), typeof(Stream), typeof(StreamedImage), new PropertyMetadata(default(Stream), ImageStreamChanged));

	public static readonly DependencyProperty ErrorBrushProperty = DependencyProperty.Register(
		nameof(ErrorBrush), typeof(Brush), typeof(StreamedImage), new PropertyMetadata(new SolidColorBrush(Colors.Red)));

	public Brush ErrorBrush
	{
		get => (Brush) GetValue(ErrorBrushProperty);
		set => SetValue(ErrorBrushProperty, value);
	}

	private static void ImageStreamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is StreamedImage streamedImage) streamedImage.LoadFromStreamAsync((Stream)e.NewValue);
	}

	private async void LoadFromStreamAsync(Stream stream)
	{
		ProgressRing.IsActive = true;
		PresentedImage.Visibility = Visibility.Collapsed;
		ErrorIcon.Visibility = Visibility.Collapsed;
		try
		{
			BitmapImage image = new BitmapImage();
                
			await image.SetSourceAsync(stream.AsRandomAccessStream());

			PresentedImage.Source = image;

			PresentedImage.Visibility = Visibility.Visible;
		}
		catch (Exception)
		{
			PresentedImage.Visibility = Visibility.Collapsed;
			ErrorIcon.Visibility = Visibility.Visible;
		}
		finally
		{
			ProgressRing.IsActive = false;
		}
	}

	public Stream SourceStream
	{
		get => (Stream) GetValue(SourceStreamProperty);
		set => SetValue(SourceStreamProperty, value);
	}

	public StreamedImage()
	{
		this.InitializeComponent();
	}
}
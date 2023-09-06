using System.Xml;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Markdown.Avalonia.Plugins;
using Markdown.Avalonia.Utils;

namespace Scarab.Util;

// Currently, the Svg plugin baked into Avalonia.Markdown
// doesn't use Avalonia.Svg.Skia, and ends up looking a bit
// wonky, specifically on some of the badges people put on
// GitHub READMEs. As those are the only markdown we're displaying,
// we use this instead.
public class SvgPlugin : IMdAvPlugin
{
    private class SvgResolver : IImageResolver
    {
        public async Task<IImage?> Load(Stream stream)
        {
            // We want to process this *off* the UI thread 
            // for the sake of not hanging it
            SvgSource? src = await Task.Run(
                () =>
                {
                    if (!IsSvgFile(stream))
                        return null;

                    var src = new SvgSource();
                    src.Load(stream);

                    return src;
                }
            );

            // But, we need to create the avalonia image on the UI thread
            // as otherwise it's an invalid operation
            return await Dispatcher.UIThread.InvokeAsync(
                () => Task.FromResult(src is null ? null : new SvgImage { Source = src })
            );
        }

        private static bool IsSvgFile(Stream fileStream)
        {
            try
            {
                using var xmlReader = XmlReader.Create(fileStream);

                return xmlReader.MoveToContent() == XmlNodeType.Element &&
                       "svg".Equals(
                           xmlReader.Name,
                           StringComparison.OrdinalIgnoreCase
                       );
            }
            catch
            {
                return false;
            }
            finally
            {
                fileStream.Seek(0, SeekOrigin.Begin);
            }
        }
    }

    public void Setup(SetupInfo info)
    {
        info.Register(new SvgResolver());
    }
}
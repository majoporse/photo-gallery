namespace Gallery.Application.Uploads;

/// <summary>The set of open admin event streams a status change is fanned out to.</summary>
public interface IUploadStream
{
    void Publish(UploadView view);
}

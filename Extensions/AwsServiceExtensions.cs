using Amazon;
using Amazon.S3;

namespace ShaloTrack_API.Extensions;

public static class AwsServiceExtensions
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(RegionEndpoint.APSoutheast1));
        return services;
    }
}
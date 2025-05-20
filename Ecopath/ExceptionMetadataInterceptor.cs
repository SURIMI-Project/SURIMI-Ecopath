using Grpc.Core.Interceptors;
using Grpc.Core;

namespace Ecopath
{
    public class ExceptionMetadataInterceptor : Interceptor
    {
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (RpcException)
            {
                // If it's a known RpcException, we don't need to do anything special
                // meta data is allready set
                throw;
            }
            catch (Exception ex)
            {
                var status = new Status(StatusCode.Internal, ex.Message);
                var metadata = new Metadata
                {
                    { "method", context.Method },
                    { "application", typeof(Program).Assembly.GetName().Name }
                };
                throw new RpcException(status, metadata);
            }
        }
    }
}

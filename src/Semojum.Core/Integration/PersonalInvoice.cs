using System.Net;
using System.Net.Http.Headers;

namespace Semojum.Core.Integration;

public sealed partial class SemojumApiClient
{
    public async Task<byte[]> DownloadPersonalInvoiceAsync(Guid orderId, CancellationToken ct)
    {
        var before = Volatile.Read(ref session) ?? throw AuthenticationRequired();
        async Task<HttpResponseMessage> OpenAsync(Session auth)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, $"/api/members/me/billing/orders/{orderId:D}/invoice"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        var response = await OpenAsync(before).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            response = await OpenAsync(await RefreshAsync(before, ct).ConfigureAwait(false)).ConfigureAwait(false);
        }
        using (response)
        {
            response.EnsureSuccessStatusCode();
            const int maximumBytes = 16 * 1024 * 1024;
            if (response.Content.Headers.ContentType?.MediaType != "application/pdf" || response.Content.Headers.ContentLength > maximumBytes)
                throw new InvalidDataException("청구서 PDF 형식이나 크기를 확인해 주세요.");
            await response.Content.LoadIntoBufferAsync(maximumBytes, ct).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) || Volatile.Read(ref session)?.Id != before.Id)
                throw new InvalidDataException("현재 계정의 청구서를 불러올 수 없습니다.");
            return bytes;
        }
    }
}

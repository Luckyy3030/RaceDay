
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RaceDay.Mvc.Services;

public class ApiService
{
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _accessor;

    private readonly JsonSerializerOptions _options =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public string LastError { get; private set; } = "";

    public ApiService(
        HttpClient client,
        IHttpContextAccessor accessor)
    {
        _client = client;
        _accessor = accessor;
    }

    private void AddToken()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var token = _accessor.HttpContext?
            .Session.GetString("Token");

        if (!string.IsNullOrWhiteSpace(token))
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        AddToken();

        var response = await _client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return default;

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(json, _options);
    }

    public async Task<byte[]?> GetBytesAsync(string url)
    {
        AddToken();

        var response = await _client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<bool> PostAsync<T>(string url, T data)
    {
        LastError = "";
        AddToken();

        try
        {
            var json = JsonSerializer.Serialize(data);

            using var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            var response = await _client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                LastError =
                    $"API returned {(int)response.StatusCode} " +
                    $"{response.ReasonPhrase}: " +
                    await response.Content.ReadAsStringAsync();

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async Task<bool> PutAsync<T>(string url, T data)
    {
        AddToken();

        var json = JsonSerializer.Serialize(data);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _client.PutAsync(url, content);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string url)
    {
        AddToken();

        var response = await _client.DeleteAsync(url);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UploadAsync(
        string url,
        IFormFile file)
    {
        AddToken();

        using var form = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        using var content = new StreamContent(stream);

        content.Headers.ContentType =
            new MediaTypeHeaderValue(file.ContentType);

        form.Add(content, "file", file.FileName);

        var response = await _client.PostAsync(url, form);

        return response.IsSuccessStatusCode;
    }

    public async Task<(byte[] Data, string ContentType)?> GetFileAsync(string url)
    {
        AddToken();

        var response = await _client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var data = await response.Content.ReadAsByteArrayAsync();

        var contentType =
            response.Content.Headers.ContentType?.MediaType
            ?? "application/octet-stream";

        return (data, contentType);
    }

}

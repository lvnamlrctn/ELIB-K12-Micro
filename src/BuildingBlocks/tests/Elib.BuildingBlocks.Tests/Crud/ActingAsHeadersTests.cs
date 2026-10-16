using System.Net;
using System.Security.Claims;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Elib.BuildingBlocks.Tests.Crud;

/// <summary>Người thao tác thật của lời gọi nội bộ: bên gửi (<see cref="ActingAsHeaders.Apply"/>) và bên nhận (<see cref="ActingAsHeaders.Read"/>) khớp nhau.</summary>
public sealed class ActingAsHeadersTests
{
    private static DefaultHttpContext StaffContext(string name = "Nguyễn Thị Mai", string userName = "mai.nt", string ip = "10.1.2.3")
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", name), new Claim("preferred_username", userName)], "test")),
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static HeaderDictionary Forwarded(HttpRequestMessage request)
    {
        var headers = new HeaderDictionary();
        foreach (var header in request.Headers) headers[header.Key] = header.Value.ToArray();
        return headers;
    }

    [Fact]
    public void Staff_request_round_trips_id_kind_name_and_ip()
    {
        var actor = new CurrentActor();
        actor.Set(4321, ElibSubjectTypes.Staff);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://holdings/internal");
        ActingAsHeaders.Apply(request, actor, StaffContext());

        var origin = ActingAsHeaders.Read(Forwarded(request));
        Assert.NotNull(origin);
        Assert.Equal((4321L, "staff", "Nguyễn Thị Mai (mai.nt)", "10.1.2.3"), (origin.ActorId, origin.ActorKind, origin.ActorName, origin.IpAddress));
    }

    [Fact]
    public void Header_values_are_ascii_so_vietnamese_names_survive_http()
    {
        var actor = new CurrentActor();
        actor.Set(1, ElibSubjectTypes.Staff);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://holdings/internal");
        ActingAsHeaders.Apply(request, actor, StaffContext(name: "Đỗ Thị Ánh"));
        Assert.All(request.Headers.SelectMany(h => h.Value), v => Assert.True(v.All(char.IsAscii), v));
        Assert.Equal("Đỗ Thị Ánh (mai.nt)", ActingAsHeaders.Read(Forwarded(request))!.ActorName);
    }

    [Theory]
    [InlineData(ElibSubjectTypes.Service)]
    [InlineData("system")]
    [InlineData("anonymous")]
    public void Jobs_and_service_accounts_forward_nothing(string kind)
    {
        var actor = new CurrentActor();
        actor.Set(7, kind);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://holdings/internal");
        ActingAsHeaders.Apply(request, actor, StaffContext());
        Assert.Empty(request.Headers);
        using var noActor = new HttpRequestMessage(HttpMethod.Get, "http://holdings/internal");
        ActingAsHeaders.Apply(noActor, null, null);
        ActingAsHeaders.Apply(noActor, new CurrentActor(), null); // chưa có người thao tác (id null)
        Assert.Empty(noActor.Headers);
    }

    [Theory]
    [InlineData("admin:1")]
    [InlineData("staff:abc")]
    [InlineData("staff:-5")]
    [InlineData("staff")]
    [InlineData("")]
    public void Malformed_or_unknown_actor_headers_are_ignored(string value)
    {
        var headers = new HeaderDictionary { [ActingAsHeaders.Actor] = value };
        Assert.Null(ActingAsHeaders.Read(headers));
    }

    [Fact]
    public void Broken_ip_is_dropped_but_the_actor_still_counts()
    {
        var headers = new HeaderDictionary
        {
            [ActingAsHeaders.Actor] = "reader:9", [ActingAsHeaders.Name] = "%E0%A4%A", [ActingAsHeaders.Ip] = "not-an-ip",
        };
        var origin = ActingAsHeaders.Read(headers);
        Assert.NotNull(origin);
        Assert.Equal((9L, "reader", (string?)null), (origin.ActorId, origin.ActorKind, origin.IpAddress));
    }
}

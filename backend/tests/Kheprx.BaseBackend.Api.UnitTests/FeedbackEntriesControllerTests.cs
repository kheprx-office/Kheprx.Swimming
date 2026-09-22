using System.Security.Claims;
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class FeedbackEntriesControllerTests
{
    private static FeedbackEntriesController Controller(IFeedbackService svc, IUserService users, Guid? userId = null)
    {
        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(new[] { new Claim("sub", userId.Value.ToString()) }, "jwt");
        return new FeedbackEntriesController(svc, users)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static FeedbackEntryDto Dto(Guid id, Guid authorId) =>
        new(id, Guid.NewGuid(), (short)4, Guid.NewGuid(), "note", authorId, string.Empty, null, new DateOnly(2024, 10, 4));

    private static CreateFeedbackEntryRequest Req() => new(4, Guid.NewGuid(), "note");

    [Fact]
    public async Task List_returns_200_and_resolves_author_names()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        svc.Setup(s => s.ListAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid(), authorId) });
        users.Setup(u => u.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<Guid, UserNameDto> { [authorId] = new(authorId, "Coach Omar", "الكابتن عمر") });

        var result = await Controller(svc.Object, users.Object).List(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<FeedbackEntryDto>>>(ok.Value);
        Assert.Equal("Coach Omar", body.Data![0].AuthorNameEn);
    }

    [Fact]
    public async Task Create_uses_current_user_as_author_and_returns_201()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(swimmerId, It.IsAny<CreateFeedbackEntryRequest>(), authorId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(Dto(Guid.NewGuid(), authorId));
        users.Setup(u => u.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<Guid, UserNameDto> { [authorId] = new(authorId, "Coach Omar", null) });

        var result = await Controller(svc.Object, users.Object, authorId).Create(swimmerId, Req(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        svc.Verify(s => s.CreateAsync(swimmerId, It.IsAny<CreateFeedbackEntryRequest>(), authorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_returns_404_when_null()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(swimmerId, It.IsAny<Guid>(), It.IsAny<CreateFeedbackEntryRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((FeedbackEntryDto?)null);

        var nf = await Controller(svc.Object, users.Object).Update(swimmerId, Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        var eid = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(swimmerId, eid, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(swimmerId, It.Is<Guid>(g => g != eid), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object, users.Object).Delete(swimmerId, eid, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object, users.Object).Delete(swimmerId, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}

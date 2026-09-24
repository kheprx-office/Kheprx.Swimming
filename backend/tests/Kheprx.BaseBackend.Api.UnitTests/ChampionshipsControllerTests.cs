using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Globalization;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ChampionshipsControllerTests
{
    private static ChampionshipsController NewController(IChampionshipService svc, IReferenceService reference)
        => new(svc, reference) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    [Fact]
    public async Task List_returns_200_and_resolves_status_code_and_names()
    {
        var statusId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[]
           {
               new CompetitionEventDto(Guid.NewGuid(), "Nats", null,
                   new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16),
                   "Cairo", null, statusId, "", "", null),
           });
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new CodedLookupDto(statusId, "upcoming", "Upcoming", "قادمة") });

        var controller = new ChampionshipsController(svc.Object, reference.Object);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CompetitionEventDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("upcoming", body.Data![0].StatusCode);
        Assert.Equal("Upcoming", body.Data![0].StatusNameEn);
        Assert.Equal("قادمة", body.Data![0].StatusNameAr);
    }

    [Fact]
    public async Task List_leaves_status_fields_empty_when_status_id_is_unknown()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[]
           {
               new CompetitionEventDto(Guid.NewGuid(), "Orphan", null,
                   new DateOnly(2024, 1, 20), new DateOnly(2024, 1, 21),
                   "Giza", null, Guid.NewGuid(), "", "", null),
           });
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<CodedLookupDto>());

        var controller = new ChampionshipsController(svc.Object, reference.Object);
        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CompetitionEventDto>>>(ok.Value);
        Assert.Equal("", body.Data![0].StatusCode);   // no throw, graceful
    }

    [Fact]
    public async Task Create_returns_201_resolves_upcoming_and_maps_english_by_default()
    {
        var upcomingId = Guid.NewGuid();
        CreateCompetitionEventCommand? cmd = null;
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateCompetitionEventCommand>(), It.IsAny<CancellationToken>()))
           .Callback<CreateCompetitionEventCommand, CancellationToken>((c, _) => cmd = c)
           .ReturnsAsync((CreateCompetitionEventCommand c, CancellationToken _) =>
               new CompetitionEventDto(Guid.NewGuid(), c.NameEn, c.NameAr, c.StartDate, c.EndDate,
                   c.LocationEn, c.LocationAr, c.StatusId, "", "", null));
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new CodedLookupDto(upcomingId, "upcoming", "Upcoming", "قادمة") });

        var request = new CreateCompetitionEventRequest("  Summer Cup  ", new DateOnly(2024, 6, 1), new DateOnly(2024, 6, 3), "  Cairo Pool  ");
        var result = await NewController(svc.Object, reference.Object).Create(request, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var body = Assert.IsType<ApiResponse<CompetitionEventDto>>(created.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal("upcoming", body.Data!.StatusCode);
        Assert.Equal("Upcoming", body.Data!.StatusNameEn);
        // English culture (default): trimmed text → En columns, Ar left null
        Assert.NotNull(cmd);
        Assert.Equal("Summer Cup", cmd!.NameEn);
        Assert.Null(cmd.NameAr);
        Assert.Equal("Cairo Pool", cmd.LocationEn);
        Assert.Null(cmd.LocationAr);
        Assert.Equal(upcomingId, cmd.StatusId);
    }

    [Theory]
    [InlineData("", "Cairo", 2024, 6, 1, 2024, 6, 3)]      // blank name
    [InlineData("Cup", "", 2024, 6, 1, 2024, 6, 3)]        // blank location
    [InlineData("Cup", "Cairo", 2024, 6, 3, 2024, 6, 1)]   // end before start
    public async Task Create_returns_400_on_invalid_request(string name, string location,
        int sy, int sm, int sd, int ey, int em, int ed)
    {
        var svc = new Mock<IChampionshipService>();
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new CodedLookupDto(Guid.NewGuid(), "upcoming", "Upcoming", "قادمة") });

        var request = new CreateCompetitionEventRequest(name, new DateOnly(sy, sm, sd), new DateOnly(ey, em, ed), location);
        var result = await NewController(svc.Object, reference.Object).Create(request, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<CompetitionEventDto>>(bad.Value);
        Assert.False(body.SuccessStatus);
        svc.Verify(s => s.CreateAsync(It.IsAny<CreateCompetitionEventCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_returns_400_when_upcoming_status_missing()
    {
        var svc = new Mock<IChampionshipService>();
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<CodedLookupDto>());

        var request = new CreateCompetitionEventRequest("Cup", new DateOnly(2024, 6, 1), new DateOnly(2024, 6, 3), "Cairo");
        var result = await NewController(svc.Object, reference.Object).Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        svc.Verify(s => s.CreateAsync(It.IsAny<CreateCompetitionEventCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_mirrors_text_into_arabic_column_under_arabic_culture()
    {
        var prev = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("ar");
        try
        {
            CreateCompetitionEventCommand? cmd = null;
            var svc = new Mock<IChampionshipService>();
            svc.Setup(s => s.CreateAsync(It.IsAny<CreateCompetitionEventCommand>(), It.IsAny<CancellationToken>()))
               .Callback<CreateCompetitionEventCommand, CancellationToken>((c, _) => cmd = c)
               .ReturnsAsync((CreateCompetitionEventCommand c, CancellationToken _) =>
                   new CompetitionEventDto(Guid.NewGuid(), c.NameEn, c.NameAr, c.StartDate, c.EndDate,
                       c.LocationEn, c.LocationAr, c.StatusId, "", "", null));
            var reference = new Mock<IReferenceService>();
            reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new[] { new CodedLookupDto(Guid.NewGuid(), "upcoming", "Upcoming", "قادمة") });

            var request = new CreateCompetitionEventRequest("بطولة الشتاء", new DateOnly(2024, 6, 1), new DateOnly(2024, 6, 3), "القاهرة");
            await NewController(svc.Object, reference.Object).Create(request, CancellationToken.None);

            Assert.NotNull(cmd);
            Assert.Equal("بطولة الشتاء", cmd!.NameEn);   // required En anchor is always set
            Assert.Equal("بطولة الشتاء", cmd.NameAr);     // Ar mirrored under ar culture
            Assert.Equal("القاهرة", cmd.LocationAr);
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }

    [Fact]
    public async Task GetById_returns_200_and_resolves_status()
    {
        var id = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new CompetitionEventDto(id, "Nats", null, new DateOnly(2023, 11, 15),
               new DateOnly(2023, 11, 16), "Cairo", null, statusId, "", "", null));
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new CodedLookupDto(statusId, "upcoming", "Upcoming", "قادمة") });

        var result = await NewController(svc.Object, reference.Object).GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<CompetitionEventDto>>(ok.Value);
        Assert.Equal("upcoming", body.Data!.StatusCode);
        Assert.Equal("Upcoming", body.Data!.StatusNameEn);
    }

    [Fact]
    public async Task GetById_returns_404_when_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEventDto?)null);
        var reference = new Mock<IReferenceService>();

        var result = await NewController(svc.Object, reference.Object).GetById(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task GetEnrollments_returns_200_with_ids()
    {
        var eventId = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetEnrolledSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { s1 });

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetEnrollments(eventId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<Guid>>>(ok.Value);
        Assert.Equal(new[] { s1 }, body.Data!);
    }

    [Fact]
    public async Task GetEnrollments_returns_404_when_event_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetEnrolledSwimmerIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Guid>?)null);

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetEnrollments(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task SetEnrollments_returns_200_when_replaced()
    {
        var eventId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetEnrollmentsAsync(eventId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new SetEnrollmentsRequest(new[] { Guid.NewGuid() });
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetEnrollments(eventId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetEnrollments_returns_404_when_event_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetEnrollmentsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var request = new SetEnrollmentsRequest(Array.Empty<Guid>());
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetEnrollments(Guid.NewGuid(), request, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public void SetEnrollments_is_restricted_to_head_coach_and_captain()
    {
        var attr = typeof(ChampionshipsController).GetMethod(nameof(ChampionshipsController.SetEnrollments))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();
        Assert.Equal("head_coach,captain", attr.Roles);
    }

    [Fact]
    public async Task GetResults_returns_200_with_rows()
    {
        var eventId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetResultsAsync(eventId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ResultsDto(new[] { new RaceResultDto(Guid.NewGuid(), Guid.NewGuid(), 24560, 0, true) }));

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetResults(eventId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<ResultsDto>>(ok.Value);
        Assert.Single(body.Data!.Results);
    }

    [Fact]
    public async Task GetResults_returns_404_when_event_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetResultsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ResultsDto?)null);

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetResults(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task SetRaceResults_returns_200_when_saved()
    {
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetRaceResultsAsync(eventId, raceId, It.IsAny<IReadOnlyList<SetRaceResultsEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SetRaceResultsResult(SetRaceResultsOutcome.Ok, new ResultsDto(Array.Empty<RaceResultDto>()), null));

        var request = new SetRaceResultsRequest(new[] { new SetRaceResultsEntry(Guid.NewGuid(), 25000) });
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetRaceResults(eventId, raceId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetRaceResults_returns_404_when_not_found()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetRaceResultsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<SetRaceResultsEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SetRaceResultsResult(SetRaceResultsOutcome.NotFound, null, null));

        var request = new SetRaceResultsRequest(Array.Empty<SetRaceResultsEntry>());
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetRaceResults(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task SetRaceResults_returns_400_when_invalid()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetRaceResultsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<SetRaceResultsEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "not_assigned"));

        var request = new SetRaceResultsRequest(new[] { new SetRaceResultsEntry(Guid.NewGuid(), 25000) });
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetRaceResults(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void SetRaceResults_is_restricted_to_head_coach_and_captain()
    {
        var attr = typeof(ChampionshipsController).GetMethod(nameof(ChampionshipsController.SetRaceResults))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();
        Assert.Equal("head_coach,captain", attr.Roles);
    }

    [Fact]
    public async Task GetSwimmerHistory_returns_200_with_the_service_rows()
    {
        var swimmerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetSwimmerHistoryAsync(swimmerId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[]
           {
               new ChampionshipSwimmerHistoryDto(eventId, "National", null,
                   new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16), "Cairo", null,
                   new[] { new ChampionshipSwimmerRaceDto("Day 1", null, Guid.NewGuid(), Guid.NewGuid(), 52340, true) }),
           });
        var reference = new Mock<IReferenceService>();

        var controller = NewController(svc.Object, reference.Object);
        var result = await controller.GetSwimmerHistory(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal(eventId, body.Data![0].EventId);
        Assert.Single(body.Data![0].Races);
        Assert.True(body.Data![0].Races[0].IsPersonalBest);
    }

    [Fact]
    public async Task GetSwimmerHistory_returns_200_and_empty_list_for_a_swimmer_with_no_history()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetSwimmerHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Array.Empty<ChampionshipSwimmerHistoryDto>());
        var controller = NewController(svc.Object, new Mock<IReferenceService>().Object);

        var result = await controller.GetSwimmerHistory(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>>(ok.Value);
        Assert.Empty(body.Data!);
    }
}

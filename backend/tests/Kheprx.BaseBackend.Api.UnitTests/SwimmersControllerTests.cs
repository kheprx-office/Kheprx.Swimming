using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class SwimmersControllerTests
{
    private static SwimmersController OnboardingController(ISwimmerService svc)
        => new(svc) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    [Fact]
    public async Task Count_returns_200_with_swimmer_count()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerCountDto(24));

        var result = await new SwimmersController(svc.Object).Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerCountDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal(24, body.Data!.Count);
    }

    [Fact]
    public async Task Create_returns_201_with_created_swimmer()
    {
        var svc = new Mock<ISwimmerService>();
        var created = new CreatedSwimmerDto(Guid.NewGuid(), "SW-0007", "mona.ali", "Mona Ali", "Oasis2026!");
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateSwimmerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), new[] { Guid.NewGuid() }, null, null, null, null);

        var result = await new SwimmersController(svc.Object).Create(req, CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
        var body = Assert.IsType<ApiResponse<CreatedSwimmerDto>>(ok.Value);
        Assert.Equal("SW-0007", body.Data!.Uid);
    }

    [Fact]
    public async Task Create_returns_409_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateSwimmerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((CreatedSwimmerDto?)null);
        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), new[] { Guid.NewGuid() }, null, null, null, null);

        var result = await new SwimmersController(svc.Object).Create(req, CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task List_returns_200_with_swimmers()
    {
        var svc = new Mock<ISwimmerService>();
        var rows = new List<SwimmerListItemDto>
        {
            new(Guid.NewGuid(), "SW-0001", "Alpha", null, "Oasis Main", null, "male", 15),
        };
        svc.Setup(s => s.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(rows);

        var result = await new SwimmersController(svc.Object).List("al", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<SwimmerListItemDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
        Assert.Equal("SW-0001", body.Data![0].Uid);
    }

    [Fact]
    public async Task GetById_returns_200_with_profile()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        var identity = new SwimmerIdentityDto(id, "SW-0001", "Alpha", null, null, 15, "male", null, "Oasis Main", null);
        svc.Setup(s => s.GetProfileAsync(id, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerProfileDto(identity, null));

        var result = await new SwimmersController(svc.Object).GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerProfileDto>>(ok.Value);
        Assert.Equal("SW-0001", body.Data!.Identity.Uid);
    }

    [Fact]
    public async Task GetById_returns_404_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SwimmerProfileDto?)null);

        var result = await new SwimmersController(svc.Object).GetById(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task UpdateIdentity_returns_200_when_updated()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpdateIdentityAsync(It.IsAny<Guid>(), It.IsAny<UpdateSwimmerIdentityRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);
        var req = new UpdateSwimmerIdentityRequest("New Name", null, new DateOnly(2010, 1, 1), "01000000009");

        var result = await new SwimmersController(svc.Object).UpdateIdentity(Guid.NewGuid(), req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateIdentity_returns_404_when_not_found()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpdateIdentityAsync(It.IsAny<Guid>(), It.IsAny<UpdateSwimmerIdentityRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(false);
        var req = new UpdateSwimmerIdentityRequest("New Name", null, new DateOnly(2010, 1, 1), null);

        var result = await new SwimmersController(svc.Object).UpdateIdentity(Guid.NewGuid(), req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task CreateExam_returns_201_with_vitals()
    {
        var svc = new Mock<ISwimmerService>();
        var im = new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق");
        svc.Setup(s => s.CreateExamAsync(It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerVitalsDto(Guid.NewGuid(), new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im));
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await new SwimmersController(svc.Object).CreateExam(Guid.NewGuid(), req, CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
    }

    [Fact]
    public async Task CreateExam_returns_404_when_swimmer_missing()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CreateExamAsync(It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SwimmerVitalsDto?)null);
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await new SwimmersController(svc.Object).CreateExam(Guid.NewGuid(), req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task ListExams_returns_200_with_exams()
    {
        var svc = new Mock<ISwimmerService>();
        var im = new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق");
        svc.Setup(s => s.ListExamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<SwimmerVitalsDto> { new(Guid.NewGuid(), new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im) });

        var result = await new SwimmersController(svc.Object).ListExams(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<SwimmerVitalsDto>>>(ok.Value);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task ListExams_returns_404_when_swimmer_missing()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.ListExamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((IReadOnlyList<SwimmerVitalsDto>?)null);
        var result = await new SwimmersController(svc.Object).ListExams(Guid.NewGuid(), CancellationToken.None);
        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task UpdateExam_returns_200_with_vitals()
    {
        var svc = new Mock<ISwimmerService>();
        var im = new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق");
        svc.Setup(s => s.UpdateExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerVitalsDto(Guid.NewGuid(), new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im));
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await new SwimmersController(svc.Object).UpdateExam(Guid.NewGuid(), Guid.NewGuid(), req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateExam_returns_404_when_not_found()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpdateExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SwimmerVitalsDto?)null);
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var result = await new SwimmersController(svc.Object).UpdateExam(Guid.NewGuid(), Guid.NewGuid(), req, CancellationToken.None);
        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task DeleteExam_returns_200_when_deleted()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.DeleteExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new SwimmersController(svc.Object).DeleteExam(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteExam_returns_404_when_missing()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.DeleteExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await new SwimmersController(svc.Object).DeleteExam(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task GetGuardians_returns_200_with_guardians()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        var dto = new SwimmerGuardiansDto(
            new GuardianDto(Guid.NewGuid(), "father", "Hassan Ali", "27001010123456", "+201009876543"),
            new GuardianDto(Guid.NewGuid(), "mother", "Fatima Ibrahim", "27505050123456", "+201005554444"));
        svc.Setup(s => s.GetGuardiansAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new SwimmersController(svc.Object).GetGuardians(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerGuardiansDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal("Hassan Ali", body.Data!.Father!.Name);
    }

    [Fact]
    public async Task GetGuardians_returns_404_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetGuardiansAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerGuardiansDto?)null);

        var result = await new SwimmersController(svc.Object).GetGuardians(id, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task UpsertGuardians_returns_200_when_saved()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.UpsertGuardiansAsync(id, It.IsAny<UpsertGuardiansRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var req = new UpsertGuardiansRequest(
            new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
            new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

        var result = await new SwimmersController(svc.Object).UpsertGuardians(id, req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.True(body.SuccessStatus);
    }

    [Fact]
    public async Task UpsertGuardians_returns_404_when_service_returns_false()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.UpsertGuardiansAsync(id, It.IsAny<UpsertGuardiansRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var req = new UpsertGuardiansRequest(
            new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
            new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

        var result = await new SwimmersController(svc.Object).UpsertGuardians(id, req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task GetLatestBodyMeasurement_returns_200_with_latest()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        var dto = new SwimmerBodyMeasurementDto(
            new BodyMeasurementDto(Guid.NewGuid(), new DateOnly(2026, 9, 19), 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));
        svc.Setup(s => s.GetBodyMeasurementAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new SwimmersController(svc.Object).GetLatestBodyMeasurement(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerBodyMeasurementDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal(78.5m, body.Data!.Latest!.RightArmCm);
    }

    [Fact]
    public async Task GetLatestBodyMeasurement_returns_404_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetBodyMeasurementAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerBodyMeasurementDto?)null);

        var result = await new SwimmersController(svc.Object).GetLatestBodyMeasurement(id, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task CreateBodyMeasurement_returns_200_when_saved()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.AddBodyMeasurementAsync(id, It.IsAny<CreateBodyMeasurementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        var result = await new SwimmersController(svc.Object).CreateBodyMeasurement(id, req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.True(body.SuccessStatus);
    }

    [Fact]
    public async Task CreateBodyMeasurement_returns_404_when_service_returns_false()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.AddBodyMeasurementAsync(id, It.IsAny<CreateBodyMeasurementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        var result = await new SwimmersController(svc.Object).CreateBodyMeasurement(id, req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingPrefill_returns_200_with_dto()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetOnboardingPrefillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new OnboardingPrefillDto("SW-0001", "Sam", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid()));

        var result = await OnboardingController(svc.Object).GetOnboardingPrefill(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<OnboardingPrefillDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal("SW-0001", body.Data!.Uid);
    }

    [Fact]
    public async Task GetOnboardingPrefill_returns_404_when_not_a_swimmer()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetOnboardingPrefillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((OnboardingPrefillDto?)null);

        var result = await OnboardingController(svc.Object).GetOnboardingPrefill(CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_returns_200_when_ok_404_when_null()
    {
        var req = new CompleteIdentityVitalsRequest("Sam", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
            new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var okSvc = new Mock<ISwimmerService>();
        okSvc.Setup(s => s.CompleteIdentityVitalsAsync(It.IsAny<Guid>(), It.IsAny<CompleteIdentityVitalsRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new OnboardingStepResultDto(false));
        var okResult = await OnboardingController(okSvc.Object).CompleteOnboardingIdentityVitals(req, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(okResult.Result);
        Assert.False(Assert.IsType<ApiResponse<OnboardingStepResultDto>>(ok.Value).Data!.MustChangePassword);

        var nfSvc = new Mock<ISwimmerService>();
        nfSvc.Setup(s => s.CompleteIdentityVitalsAsync(It.IsAny<Guid>(), It.IsAny<CompleteIdentityVitalsRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((OnboardingStepResultDto?)null);
        var nfResult = await OnboardingController(nfSvc.Object).CompleteOnboardingIdentityVitals(req, CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nfResult.Result).StatusCode);
    }
}

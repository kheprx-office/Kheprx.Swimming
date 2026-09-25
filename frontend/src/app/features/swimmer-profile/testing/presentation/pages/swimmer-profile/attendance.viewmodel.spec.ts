import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SwimmerProfileViewModel } from '@features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel';
import { GetMySwimmerIdUseCase } from '@features/swimmer-profile/domain/usecases/get-my-swimmer-id.use-case';
import { ok } from '@core/domain/result/result';
import { GetSwimmerProfileUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case';
import { UpdateSwimmerIdentityUseCase } from '@features/swimmer-profile/domain/usecases/update-swimmer-identity.use-case';
import { CreateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/create-medical-exam.use-case';
import { ListMedicalExamsUseCase } from '@features/swimmer-profile/domain/usecases/list-medical-exams.use-case';
import { UpdateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/update-medical-exam.use-case';
import { DeleteMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/delete-medical-exam.use-case';
import { LoadBloodTypesUseCase } from '@features/reference/domain/usecases/load-blood-types.use-case';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { GetSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-guardians.use-case';
import { UpsertSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/upsert-swimmer-guardians.use-case';
import { GetLatestBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case';
import { CreateBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/create-body-measurement.use-case';
import { ListInBodyReadingsUseCase } from '@features/swimmer-profile/domain/usecases/list-inbody-readings.use-case';
import { CreateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/create-inbody-reading.use-case';
import { UpdateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/update-inbody-reading.use-case';
import { DeleteInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case';
import { ListRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-records.use-case';
import { UpdateRecordUseCase } from '@features/swimmer-profile/domain/usecases/update-record.use-case';
import { DeleteRecordUseCase } from '@features/swimmer-profile/domain/usecases/delete-record.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { ListHealthReadingsUseCase } from '@features/health-readings/domain/usecases/list-health-readings.use-case';
import { UpdateHealthReadingUseCase } from '@features/health-readings/domain/usecases/update-health-reading.use-case';
import { DeleteHealthReadingUseCase } from '@features/health-readings/domain/usecases/delete-health-reading.use-case';
import { ListFeedbackEntriesUseCase } from '@features/swimmer-profile/domain/usecases/list-feedback-entries.use-case';
import { CreateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/create-feedback-entry.use-case';
import { UpdateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/update-feedback-entry.use-case';
import { DeleteFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/delete-feedback-entry.use-case';
import { LoadFeedbackCategoriesUseCase } from '@features/reference/domain/usecases/load-feedback-categories.use-case';
import { ListAttendanceRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-attendance-records.use-case';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { LoadSwimmerChampionshipHistoryUseCase } from '@features/championships/domain/usecases/load-swimmer-championship-history.use-case';
import { LoadDistancesUseCase } from '@features/reference/domain/usecases/load-distances.use-case';
import { LoadStrokesUseCase } from '@features/reference/domain/usecases/load-strokes.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

// ────────────────────────────────────────────────────────────────────────────
// Shared stubs
// ────────────────────────────────────────────────────────────────────────────

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const IDENTITY = {
  id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: 'أحمد', dob: '2010-01-01',
  age: 16, genderCode: 'male', phone: '01000000001',
  trainingClubNameEn: 'Oasis', trainingClubNameAr: null,
};
const VITALS = {
  id: 'e1', examDate: '2026-09-19', bloodType: null,
  hemoglobin: 14.8, heightCm: 182, weightKg: 74,
  internalMed: REF, heartAssess: REF, spineAssess: REF,
};

// ────────────────────────────────────────────────────────────────────────────
// Attendance test data — deterministic ISO dates, no "today" dependency
// ────────────────────────────────────────────────────────────────────────────

// Statuses
const ST_PRESENT  = { id: 'st1', code: 'present',  nameEn: 'Present',  nameAr: 'حاضر' };
const ST_ABSENT   = { id: 'st2', code: 'absent',   nameEn: 'Absent',   nameAr: 'غائب' };
const ST_EXCUSED  = { id: 'st3', code: 'excused',  nameEn: 'Excused',  nameAr: 'معذور' };
const ST_LATE     = { id: 'st4', code: 'late',     nameEn: 'Late',     nameAr: 'متأخر' };

// Records: three in Sep-2026, one in Aug-2026
// Sep-2026: 1 present, 1 absent, 1 excused → rate = round(1/3*100) = 33
const A_EXCUSED : { id: string; sessionDate: string; statusId: string; coachNoteEn: string | null; coachNoteAr: string | null; recordedByNameEn: string; recordedByNameAr: string | null } = {
  id: 'a1', sessionDate: '2026-09-07', statusId: 'st3', coachNoteEn: 'Travel', coachNoteAr: null,
  recordedByNameEn: 'Coach', recordedByNameAr: null,
};
const A_ABSENT: { id: string; sessionDate: string; statusId: string; coachNoteEn: string | null; coachNoteAr: string | null; recordedByNameEn: string; recordedByNameAr: string | null } = {
  id: 'a2', sessionDate: '2026-09-09', statusId: 'st2', coachNoteEn: null, coachNoteAr: null,
  recordedByNameEn: 'Coach', recordedByNameAr: null,
};
const A_PRESENT: { id: string; sessionDate: string; statusId: string; coachNoteEn: string | null; coachNoteAr: string | null; recordedByNameEn: string; recordedByNameAr: string | null } = {
  id: 'a3', sessionDate: '2026-09-11', statusId: 'st1', coachNoteEn: null, coachNoteAr: null,
  recordedByNameEn: 'Coach', recordedByNameAr: null,
};
const A_AUG: { id: string; sessionDate: string; statusId: string; coachNoteEn: string | null; coachNoteAr: string | null; recordedByNameEn: string; recordedByNameAr: string | null } = {
  id: 'a4', sessionDate: '2026-08-15', statusId: 'st1', coachNoteEn: null, coachNoteAr: null,
  recordedByNameEn: 'Coach', recordedByNameAr: null,
};

const ALL_RECORDS = [A_EXCUSED, A_ABSENT, A_PRESENT, A_AUG];
const ALL_STATUSES = [ST_PRESENT, ST_ABSENT, ST_EXCUSED, ST_LATE];

// ────────────────────────────────────────────────────────────────────────────
// Test harness — mirrors swimmer-profile.viewmodel.spec.ts exactly
// ────────────────────────────────────────────────────────────────────────────

function build(overrides: {
  listAttendance?: unknown;
  loadAttendanceStatuses?: unknown;
} = {}) {
  const getUc        = { run: jest.fn().mockResolvedValue({ ok: true, data: { identity: IDENTITY, vitals: VITALS } }) };
  const updateUc     = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const createUc     = { run: jest.fn().mockResolvedValue({ ok: true, data: VITALS }) };
  const listExamsUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: [VITALS] }) };
  const updateExamUc = { run: jest.fn().mockResolvedValue({ ok: true, data: VITALS }) };
  const deleteExamUc = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const bloodUc      = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const fitnessUc    = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const getGuardiansUc    = { run: jest.fn().mockResolvedValue({ ok: true, data: { father: null, mother: null } }) };
  const upsertGuardiansUc = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const getBodyMeasurementUc    = { run: jest.fn().mockResolvedValue({ ok: true, data: null }) };
  const createBodyMeasurementUc = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const listInBodyUc    = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const createInBodyUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const updateInBodyUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const deleteInBodyUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const listRecordsUc   = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const updateRecordUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const deleteRecordUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const loadObsCatsUc   = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const listHealthReadingsUc   = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const updateHealthReadingUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const deleteHealthReadingUc  = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const listFeedbackUc         = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const createFeedbackUc       = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const updateFeedbackUc       = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const deleteFeedbackUc       = { run: jest.fn().mockResolvedValue({ ok: true, data: undefined }) };
  const loadFeedbackCategoriesUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };

  const listAttendanceUc        = { run: jest.fn().mockResolvedValue(overrides.listAttendance        ?? { ok: true, data: ALL_RECORDS }) };
  const loadAttendanceStatusesUc = { run: jest.fn().mockResolvedValue(overrides.loadAttendanceStatuses ?? { ok: true, data: ALL_STATUSES }) };
  const loadChampHistoryUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const loadDistancesUc    = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const loadStrokesUc      = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };

  const getMyIdUc = { run: jest.fn().mockResolvedValue(ok('SW-ME')) };

  const notify  = { success: jest.fn(), error: jest.fn() };
  const i18n    = { t: (k: string) => k };
  const session = { role: signal<'head_coach' | 'captain' | null>('head_coach') };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      SwimmerProfileViewModel,
      { provide: GetMySwimmerIdUseCase,              useValue: getMyIdUc },
      { provide: GetSwimmerProfileUseCase,           useValue: getUc },
      { provide: UpdateSwimmerIdentityUseCase,        useValue: updateUc },
      { provide: CreateMedicalExamUseCase,            useValue: createUc },
      { provide: ListMedicalExamsUseCase,             useValue: listExamsUc },
      { provide: UpdateMedicalExamUseCase,            useValue: updateExamUc },
      { provide: DeleteMedicalExamUseCase,            useValue: deleteExamUc },
      { provide: LoadBloodTypesUseCase,               useValue: bloodUc },
      { provide: LoadFitnessAssessmentsUseCase,       useValue: fitnessUc },
      { provide: GetSwimmerGuardiansUseCase,          useValue: getGuardiansUc },
      { provide: UpsertSwimmerGuardiansUseCase,       useValue: upsertGuardiansUc },
      { provide: GetLatestBodyMeasurementUseCase,     useValue: getBodyMeasurementUc },
      { provide: CreateBodyMeasurementUseCase,        useValue: createBodyMeasurementUc },
      { provide: ListInBodyReadingsUseCase,           useValue: listInBodyUc },
      { provide: CreateInBodyReadingUseCase,          useValue: createInBodyUc },
      { provide: UpdateInBodyReadingUseCase,          useValue: updateInBodyUc },
      { provide: DeleteInBodyReadingUseCase,          useValue: deleteInBodyUc },
      { provide: ListRecordsUseCase,                  useValue: listRecordsUc },
      { provide: UpdateRecordUseCase,                 useValue: updateRecordUc },
      { provide: DeleteRecordUseCase,                 useValue: deleteRecordUc },
      { provide: LoadObservationCategoriesUseCase,    useValue: loadObsCatsUc },
      { provide: ListHealthReadingsUseCase,           useValue: listHealthReadingsUc },
      { provide: UpdateHealthReadingUseCase,          useValue: updateHealthReadingUc },
      { provide: DeleteHealthReadingUseCase,          useValue: deleteHealthReadingUc },
      { provide: ListFeedbackEntriesUseCase,          useValue: listFeedbackUc },
      { provide: CreateFeedbackEntryUseCase,          useValue: createFeedbackUc },
      { provide: UpdateFeedbackEntryUseCase,          useValue: updateFeedbackUc },
      { provide: DeleteFeedbackEntryUseCase,          useValue: deleteFeedbackUc },
      { provide: LoadFeedbackCategoriesUseCase,       useValue: loadFeedbackCategoriesUc },
      { provide: ListAttendanceRecordsUseCase,        useValue: listAttendanceUc },
      { provide: LoadAttendanceStatusesUseCase,       useValue: loadAttendanceStatusesUc },
      { provide: LoadSwimmerChampionshipHistoryUseCase, useValue: loadChampHistoryUc },
      { provide: LoadDistancesUseCase,                useValue: loadDistancesUc },
      { provide: LoadStrokesUseCase,                  useValue: loadStrokesUc },
      { provide: NotificationService,                 useValue: notify },
      { provide: TranslateService,                    useValue: i18n },
      { provide: AuthSessionStore,                    useValue: session },
    ],
  });

  return {
    vm: TestBed.inject(SwimmerProfileViewModel),
    listAttendanceUc,
    loadAttendanceStatusesUc,
  };
}

// ────────────────────────────────────────────────────────────────────────────
// Helper: trigger attendance tab and await async load
// ────────────────────────────────────────────────────────────────────────────

async function loadAttendanceTab(vm: SwimmerProfileViewModel): Promise<void> {
  vm.setTab('attendance');
  // Two micro-task ticks mirrors how the existing spec awaits lazy loaders.
  await Promise.resolve();
  await Promise.resolve();
}

// ────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────

describe('SwimmerProfileViewModel — Attendance', () => {
  it('loads records + statuses on setTab("attendance") and lazy-loads only once', async () => {
    const { vm, listAttendanceUc, loadAttendanceStatusesUc } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);

    expect(listAttendanceUc.run).toHaveBeenCalledTimes(1);
    expect(loadAttendanceStatusesUc.run).toHaveBeenCalledTimes(1);
    expect(vm.attendanceRecords()).toHaveLength(4);

    // Switching away and back must NOT reload.
    vm.setTab('identityVitals');
    vm.setTab('attendance');
    await Promise.resolve();
    expect(listAttendanceUc.run).toHaveBeenCalledTimes(1);
  });

  it('attendanceMonths lists months newest-first and selectedMonth defaults to the latest', async () => {
    const { vm } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);

    const months = vm.attendanceMonths();
    expect(months[0]).toBe('2026-09');
    expect(months[1]).toBe('2026-08');
    expect(vm.selectedMonth()).toBe('2026-09');
  });

  it('attendanceRate equals round(present/total*100) for the selected month', async () => {
    const { vm } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);

    // Sep-2026: 3 records — 1 present (st1), 1 absent (st2), 1 excused (st3)
    // attended = present only → 1; rate = round(1/3*100) = 33
    expect(vm.selectedMonth()).toBe('2026-09');
    expect(vm.attendanceRate()).toBe(33);
  });

  it('attendanceWeeks is a non-empty array of 7-cell rows; dated cells expose correct code + hasNote', async () => {
    const { vm } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);

    const weeks = vm.attendanceWeeks();
    expect(weeks.length).toBeGreaterThan(0);
    for (const week of weeks) {
      expect(week).toHaveLength(7);
    }

    // Find the cell for 2026-09-07 (excused, has note)
    const allCells = weeks.flat();
    const excusedCell = allCells.find((c) => c?.dateIso === '2026-09-07');
    expect(excusedCell).toBeDefined();
    expect(excusedCell!.code).toBe('excused');
    expect(excusedCell!.hasNote).toBe(true);

    // Find the cell for 2026-09-09 (absent, no note)
    const absentCell = allCells.find((c) => c?.dateIso === '2026-09-09');
    expect(absentCell).toBeDefined();
    expect(absentCell!.code).toBe('absent');
    expect(absentCell!.hasNote).toBe(false);

    // Find the cell for 2026-09-11 (present, no note)
    const presentCell = allCells.find((c) => c?.dateIso === '2026-09-11');
    expect(presentCell).toBeDefined();
    expect(presentCell!.code).toBe('present');
    expect(presentCell!.hasNote).toBe(false);
  });

  it('selectAttendanceDay toggles the detail; absent day resolves to absent status', async () => {
    const { vm } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);

    vm.selectAttendanceDay('2026-09-09');
    const detail = vm.selectedAttendanceDetail();
    expect(detail).not.toBeNull();
    expect(detail!.status!.code).toBe('absent');
    expect(detail!.dateIso).toBe('2026-09-09');

    // Selecting the same date again toggles it off.
    vm.selectAttendanceDay('2026-09-09');
    expect(vm.selectedAttendanceDay()).toBeNull();
    expect(vm.selectedAttendanceDetail()).toBeNull();
  });

  it('selectAttendanceMonth switches month and clears the day selection', async () => {
    const { vm } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);

    vm.selectAttendanceDay('2026-09-09');
    expect(vm.selectedAttendanceDay()).toBe('2026-09-09');

    vm.selectAttendanceMonth('2026-08');
    expect(vm.selectedMonth()).toBe('2026-08');
    expect(vm.selectedAttendanceDay()).toBeNull();
  });

  it('empty records → no months, rate 0, no weeks', async () => {
    const { vm } = build({ listAttendance: { ok: true, data: [] } });
    await vm.load('s1');
    await loadAttendanceTab(vm);

    expect(vm.attendanceMonths()).toHaveLength(0);
    expect(vm.selectedMonth()).toBe('');
    expect(vm.attendanceRate()).toBe(0);
    expect(vm.attendanceWeeks()).toHaveLength(0);
  });

  it('load() resets attendance state for a new swimmer', async () => {
    const { vm, listAttendanceUc } = build();
    await vm.load('s1');
    await loadAttendanceTab(vm);
    expect(vm.attendanceRecords()).toHaveLength(4);

    // Navigate to a different swimmer — state must be cleared.
    await vm.load('s2');
    expect(vm.attendanceRecords()).toHaveLength(0);
    expect(vm.selectedMonth()).toBe('');
    expect(vm.selectedAttendanceDay()).toBeNull();

    // Opening the attendance tab again for s2 must trigger a fresh fetch.
    vm.setTab('attendance');
    await Promise.resolve(); await Promise.resolve();
    expect(listAttendanceUc.run).toHaveBeenCalledTimes(2); // once for s1, once for s2
  });

  it('attendanceRate counts both present and late as attended', async () => {
    const lateRecord: { id: string; sessionDate: string; statusId: string; coachNoteEn: string | null; coachNoteAr: string | null; recordedByNameEn: string; recordedByNameAr: string | null } = {
      id: 'a5', sessionDate: '2026-09-13', statusId: 'st4',
      coachNoteEn: null, coachNoteAr: null,
      recordedByNameEn: 'Coach', recordedByNameAr: null,
    };
    // Sep-2026: present(a3) + late(a5) + absent(a2) + excused(a1) = 4 records
    // attended = 2 (present + late) → rate = round(2/4*100) = 50
    const { vm } = build({ listAttendance: { ok: true, data: [A_EXCUSED, A_ABSENT, A_PRESENT, lateRecord, A_AUG] } });
    await vm.load('s1');
    await loadAttendanceTab(vm);

    expect(vm.selectedMonth()).toBe('2026-09');
    expect(vm.attendanceRate()).toBe(50);
  });
});

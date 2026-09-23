import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SwimmerProfileViewModel } from '@features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel';
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
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const IDENTITY = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: 'أحمد', dob: '2010-01-01', age: 16, genderCode: 'male', phone: '01000000001', trainingClubNameEn: 'Oasis', trainingClubNameAr: null };
const VITALS = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 14.8, heightCm: 182, weightKg: 74, internalMed: REF, heartAssess: REF, spineAssess: REF };
const VITALS2 = { id: 'e2', examDate: '2024-01-01', bloodType: null, hemoglobin: 13, heightCm: 178, weightKg: 71, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(over: { profile?: unknown; update?: unknown; create?: unknown; list?: unknown; updateExam?: unknown; deleteExam?: unknown; getGuardians?: unknown; upsertGuardians?: unknown; getBodyMeasurement?: unknown; createBodyMeasurement?: unknown; listInBody?: unknown; createInBody?: unknown; updateInBody?: unknown; deleteInBody?: unknown; role?: 'head_coach' | 'captain' | null; listHealthReadings?: unknown; updateHealthReading?: unknown; deleteHealthReading?: unknown; listFeedback?: unknown; createFeedback?: unknown; updateFeedback?: unknown; deleteFeedback?: unknown; feedbackCategories?: unknown } = {}) {
  const getUc = { run: jest.fn().mockResolvedValue(over.profile ?? { ok: true, data: { identity: IDENTITY, vitals: VITALS } }) };
  const updateUc = { run: jest.fn().mockResolvedValue(over.update ?? { ok: true, data: undefined }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: VITALS }) };
  const listExamsUc = { run: jest.fn().mockResolvedValue(over.list ?? { ok: true, data: [VITALS, VITALS2] }) };
  const updateExamUc = { run: jest.fn().mockResolvedValue(over.updateExam ?? { ok: true, data: VITALS }) };
  const deleteExamUc = { run: jest.fn().mockResolvedValue(over.deleteExam ?? { ok: true, data: undefined }) };
  const bloodUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const fitnessUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [REF] }) };
  const getGuardiansUc = { run: jest.fn().mockResolvedValue(over.getGuardians ?? { ok: true, data: { father: null, mother: null } }) };
  const upsertGuardiansUc = { run: jest.fn().mockResolvedValue(over.upsertGuardians ?? { ok: true, data: undefined }) };
  const getBodyMeasurementUc = { run: jest.fn().mockResolvedValue((over as any).getBodyMeasurement ?? { ok: true, data: null }) };
  const createBodyMeasurementUc = { run: jest.fn().mockResolvedValue((over as any).createBodyMeasurement ?? { ok: true, data: undefined }) };
  const R1 = { id: 'r1', readingDate: '2024-06-15', heightCm: 180, weightKg: 72.5, fatPct: 15.2, musclePct: 40.1, waterPct: 55.0, boneDensity: 1.30, bodyDensity: 1.05 };
  const R2 = { id: 'r2', readingDate: '2024-10-04', heightCm: 181, weightKg: 74.0, fatPct: 12.8, musclePct: 42.1, waterPct: 56.5, boneDensity: 1.35, bodyDensity: 1.07 };
  const listInBodyUc = { run: jest.fn().mockResolvedValue((over as any).listInBody ?? { ok: true, data: [R2, R1] }) };   // newest first
  const createInBodyUc = { run: jest.fn().mockResolvedValue((over as any).createInBody ?? { ok: true, data: R2 }) };
  const updateInBodyUc = { run: jest.fn().mockResolvedValue((over as any).updateInBody ?? { ok: true, data: R2 }) };
  const deleteInBodyUc = { run: jest.fn().mockResolvedValue((over as any).deleteInBody ?? { ok: true, data: undefined }) };
  const CAT_A = { id: 'c1', code: 'allergy', nameEn: 'Allergy', nameAr: 'حساسية' };
  const CAT_B = { id: 'c2', code: 'surgery', nameEn: 'Surgery', nameAr: 'جراحة' };
  const REC1 = { id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe', observedDate: '2026-09-10T10:00:00Z' };
  const REC2 = { id: 'o2', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Pollen', value: 'Mild', observedDate: '2026-09-20T10:00:00Z' };
  const REC3 = { id: 'o3', swimmerId: 's1', categoryId: 'c2', fieldLabel: 'Knee', value: '2019', observedDate: '2026-09-15T10:00:00Z' };
  const listRecordsUc = { run: jest.fn().mockResolvedValue((over as any).listRecords ?? { ok: true, data: [REC2, REC3, REC1] }) };
  const updateRecordUc = { run: jest.fn().mockResolvedValue((over as any).updateRecord ?? { ok: true, data: REC2 }) };
  const deleteRecordUc = { run: jest.fn().mockResolvedValue((over as any).deleteRecord ?? { ok: true, data: undefined }) };
  const loadObsCatsUc = { run: jest.fn().mockResolvedValue((over as any).categories ?? { ok: true, data: [CAT_A, CAT_B] }) };
  const HR1 = { id: 'h1', medicalTestId: 't1', testNameEn: 'Hemoglobin', testNameAr: 'هيموجلوبين', unit: 'g/dL', value: 14.8, lowerBound: 13.5, upperBound: 17.5, readingDate: '2023-10-12T00:00:00Z', status: 'normal' };
  const HR2 = { id: 'h2', medicalTestId: 't2', testNameEn: 'Ferritin', testNameAr: 'فيريتين', unit: 'ng/mL', value: 22, lowerBound: 30, upperBound: 400, readingDate: '2023-07-15T00:00:00Z', status: 'out' };
  const listHealthReadingsUc = { run: jest.fn().mockResolvedValue((over as any).listHealthReadings ?? { ok: true, data: [HR1, HR2] }) }; // newest first
  const updateHealthReadingUc = { run: jest.fn().mockResolvedValue((over as any).updateHealthReading ?? { ok: true, data: { ...HR2, value: 35, status: 'normal' } }) };
  const deleteHealthReadingUc = { run: jest.fn().mockResolvedValue((over as any).deleteHealthReading ?? { ok: true, data: undefined }) };
  const listFeedbackUc = { run: jest.fn().mockResolvedValue((over as any).listFeedback ?? { ok: true, data: [] }) };
  const createFeedbackUc = { run: jest.fn().mockResolvedValue((over as any).createFeedback ?? { ok: true, data: undefined }) };
  const updateFeedbackUc = { run: jest.fn().mockResolvedValue((over as any).updateFeedback ?? { ok: true, data: undefined }) };
  const deleteFeedbackUc = { run: jest.fn().mockResolvedValue((over as any).deleteFeedback ?? { ok: true, data: undefined }) };
  const loadFeedbackCategoriesUc = { run: jest.fn().mockResolvedValue((over as any).feedbackCategories ?? { ok: true, data: [] }) };
  const listAttendanceUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const loadAttendanceStatusesUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  const session = { role: signal(over.role === undefined ? 'head_coach' : over.role) };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    SwimmerProfileViewModel,
    { provide: GetSwimmerProfileUseCase, useValue: getUc },
    { provide: UpdateSwimmerIdentityUseCase, useValue: updateUc },
    { provide: CreateMedicalExamUseCase, useValue: createUc },
    { provide: ListMedicalExamsUseCase, useValue: listExamsUc },
    { provide: UpdateMedicalExamUseCase, useValue: updateExamUc },
    { provide: DeleteMedicalExamUseCase, useValue: deleteExamUc },
    { provide: LoadBloodTypesUseCase, useValue: bloodUc },
    { provide: LoadFitnessAssessmentsUseCase, useValue: fitnessUc },
    { provide: GetSwimmerGuardiansUseCase, useValue: getGuardiansUc },
    { provide: UpsertSwimmerGuardiansUseCase, useValue: upsertGuardiansUc },
    { provide: GetLatestBodyMeasurementUseCase, useValue: getBodyMeasurementUc },
    { provide: CreateBodyMeasurementUseCase, useValue: createBodyMeasurementUc },
    { provide: ListInBodyReadingsUseCase, useValue: listInBodyUc },
    { provide: CreateInBodyReadingUseCase, useValue: createInBodyUc },
    { provide: UpdateInBodyReadingUseCase, useValue: updateInBodyUc },
    { provide: DeleteInBodyReadingUseCase, useValue: deleteInBodyUc },
    { provide: ListRecordsUseCase, useValue: listRecordsUc },
    { provide: UpdateRecordUseCase, useValue: updateRecordUc },
    { provide: DeleteRecordUseCase, useValue: deleteRecordUc },
    { provide: LoadObservationCategoriesUseCase, useValue: loadObsCatsUc },
    { provide: ListHealthReadingsUseCase, useValue: listHealthReadingsUc },
    { provide: UpdateHealthReadingUseCase, useValue: updateHealthReadingUc },
    { provide: DeleteHealthReadingUseCase, useValue: deleteHealthReadingUc },
    { provide: ListFeedbackEntriesUseCase, useValue: listFeedbackUc },
    { provide: CreateFeedbackEntryUseCase, useValue: createFeedbackUc },
    { provide: UpdateFeedbackEntryUseCase, useValue: updateFeedbackUc },
    { provide: DeleteFeedbackEntryUseCase, useValue: deleteFeedbackUc },
    { provide: LoadFeedbackCategoriesUseCase, useValue: loadFeedbackCategoriesUc },
    { provide: ListAttendanceRecordsUseCase, useValue: listAttendanceUc },
    { provide: LoadAttendanceStatusesUseCase, useValue: loadAttendanceStatusesUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
    { provide: AuthSessionStore, useValue: session },
  ] });
  return { vm: TestBed.inject(SwimmerProfileViewModel), getUc, updateUc, createUc, listExamsUc, updateExamUc, deleteExamUc, getGuardiansUc, upsertGuardiansUc, getBodyMeasurementUc, createBodyMeasurementUc, listInBodyUc, createInBodyUc, updateInBodyUc, deleteInBodyUc, notify, listRecordsUc, updateRecordUc, deleteRecordUc, loadObsCatsUc, listHealthReadingsUc, updateHealthReadingUc, deleteHealthReadingUc, listFeedbackUc, createFeedbackUc, updateFeedbackUc, deleteFeedbackUc, loadFeedbackCategoriesUc };
}

describe('SwimmerProfileViewModel', () => {
  it('load populates identity and vitals', async () => {
    const { vm } = build();
    await vm.load('s1');
    expect(vm.profile()?.identity.nameEn).toBe('Ahmed');
    expect(vm.profile()?.vitals?.hemoglobin).toBe(14.8);
    expect(vm.notFound()).toBe(false);
  });

  it('load sets notFound on a not_found error', async () => {
    const { vm } = build({ profile: { ok: false, error: { status: 404, kind: 'not_found' } } });
    await vm.load('sX');
    expect(vm.notFound()).toBe(true);
    expect(vm.profile()).toBeNull();
  });

  it('canEdit is false for a null role', () => {
    const { vm } = build({ role: null });
    expect(vm.canEdit()).toBe(false);
  });

  it('saveIdentity toasts success and reloads', async () => {
    const { vm, updateUc, getUc, notify } = build();
    await vm.load('s1');
    vm.startEditIdentity();
    vm.idNameEn.set('New Name');
    await vm.saveIdentity();
    expect(updateUc.run).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.identityUpdated');
    expect(getUc.run).toHaveBeenCalledTimes(2); // initial + reload
    expect(vm.editingIdentity()).toBe(false);
  });

  it('saveIdentity toasts error on failure', async () => {
    const { vm, notify } = build({ update: { ok: false, error: { status: 500 } } });
    await vm.load('s1');
    vm.startEditIdentity();
    await vm.saveIdentity();
    expect(notify.error).toHaveBeenCalledWith('swimmerProfile.toasts.saveFailed');
  });

  it('saveVitals records a new exam, toasts success and reloads', async () => {
    const { vm, createUc, notify } = build();
    await vm.load('s1');
    await vm.startNewExam();
    vm.vExamDate.set('2026-09-19');
    vm.vHemoglobin.set('15'); vm.vHeightCm.set('183'); vm.vWeightKg.set('75');
    vm.vInternalMedId.set('f1'); vm.vHeartAssessId.set('f1'); vm.vSpineAssessId.set('f1');
    expect(vm.canSaveVitals()).toBe(true);
    await vm.saveVitals();
    expect(createUc.run).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.examRecorded');
    expect(vm.editingVitals()).toBe(false);
  });

  it('loadExams populates the list and selects the latest', async () => {
    const { vm } = build();
    await vm.load('s1');
    expect(vm.exams()).toHaveLength(2);
    expect(vm.selectedExamId()).toBe('e1');
    expect(vm.selectedExam()?.id).toBe('e1');
  });

  it('changing selectedExamId updates selectedExam', async () => {
    const { vm } = build();
    await vm.load('s1');
    vm.selectedExamId.set('e2');
    expect(vm.selectedExam()?.id).toBe('e2');
  });

  it('startEditExam + saveVitals updates the selected exam', async () => {
    const { vm, updateExamUc, notify } = build();
    await vm.load('s1');
    await vm.startEditExam();
    expect(vm.editingExamId()).toBe('e1');
    await vm.saveVitals();
    expect(updateExamUc.run).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.examUpdated');
    expect(vm.editingVitals()).toBe(false);
  });

  it('delete flow confirms, deletes, toasts and reloads', async () => {
    const { vm, deleteExamUc, listExamsUc, notify } = build();
    await vm.load('s1');
    vm.askDeleteExam();
    expect(vm.confirmingDelete()).toBe(true);
    await vm.confirmDeleteExam();
    expect(deleteExamUc.run).toHaveBeenCalledWith({ id: 's1', examId: 'e1' });
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.examDeleted');
    expect(vm.confirmingDelete()).toBe(false);
    expect(listExamsUc.run).toHaveBeenCalledTimes(2); // initial load + after delete
  });

  describe('guardian tab', () => {
    it('setTab("guardian") lazy-loads guardians once', async () => {
      const { vm, getGuardiansUc } = build();
      await vm.load('s1');
      getGuardiansUc.run.mockResolvedValue({ ok: true, data: { father: null, mother: null } });
      vm.setTab('guardian');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('guardian');
      expect(getGuardiansUc.run).toHaveBeenCalledTimes(1);
      vm.setTab('identityVitals');
      vm.setTab('guardian');
      await Promise.resolve();
      expect(getGuardiansUc.run).toHaveBeenCalledTimes(1); // not reloaded
    });

    it('load() resets to Identity & Vitals and re-fetches guardians for a new swimmer', async () => {
      const { vm, getGuardiansUc } = build();
      await vm.load('s1');
      vm.setTab('guardian');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('guardian');
      expect(getGuardiansUc.run).toHaveBeenCalledTimes(1);

      // Navigate to another swimmer (same persisted view-model instance).
      await vm.load('s2');
      expect(vm.activeTab()).toBe('identityVitals');       // Symptom B: default tab restored
      expect(vm.guardians()).toBeNull();                   // stale data cleared

      // Opening Guardian for the new swimmer must fetch again, not stay blocked by a stale flag.
      vm.setTab('guardian');
      await Promise.resolve(); await Promise.resolve();
      expect(getGuardiansUc.run).toHaveBeenCalledTimes(2); // Symptom A: re-fetched for s2
    });

    it('canSaveGuardians requires all six fields and 14-digit national IDs', () => {
      const { vm } = build();
      vm.startEditGuardians();
      expect(vm.canSaveGuardians()).toBe(false);
      vm.gFatherName.set('Hassan'); vm.gFatherNationalId.set('27001010123456'); vm.gFatherPhone.set('+201009876543');
      vm.gMotherName.set('Fatima'); vm.gMotherNationalId.set('123'); vm.gMotherPhone.set('+201005554444');
      expect(vm.canSaveGuardians()).toBe(false); // mother national id invalid
      vm.gMotherNationalId.set('27505050123456');
      expect(vm.canSaveGuardians()).toBe(true);
    });
  });

  describe('physiological tab', () => {
    const M = { id: 'b1', measuredAt: '2026-09-19', rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 };

    it('setTab("physiological") lazy-loads the measurement once', async () => {
      const { vm, getBodyMeasurementUc } = build({ getBodyMeasurement: { ok: true, data: M } } as any);
      await vm.load('s1');
      vm.setTab('physiological');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('physiological');
      expect(vm.bodyMeasurement()?.rightArmCm).toBe(78.5);
      expect(getBodyMeasurementUc.run).toHaveBeenCalledTimes(1);
      vm.setTab('identityVitals');
      vm.setTab('physiological');
      await Promise.resolve();
      expect(getBodyMeasurementUc.run).toHaveBeenCalledTimes(1); // not reloaded
    });

    it('canSaveBodyMeasurement requires all seven positive, in-range numbers', () => {
      const { vm } = build();
      vm.startEditBodyMeasurement();
      expect(vm.canSaveBodyMeasurement()).toBe(false);
      vm.bmRightArm.set('78.5'); vm.bmLeftArm.set('78.2'); vm.bmRightLeg.set('96.2'); vm.bmLeftLeg.set('96');
      vm.bmTorso.set('52.8'); vm.bmBustDiameter.set('94'); vm.bmWaistDiameter.set('1000'); // out of range
      expect(vm.canSaveBodyMeasurement()).toBe(false);
      vm.bmWaistDiameter.set('76.5');
      expect(vm.canSaveBodyMeasurement()).toBe(true);
      vm.bmRightArm.set('0'); // not positive
      expect(vm.canSaveBodyMeasurement()).toBe(false);
    });

    it('saveBodyMeasurement posts, toasts success and reloads', async () => {
      const { vm, createBodyMeasurementUc, getBodyMeasurementUc, notify } = build({ getBodyMeasurement: { ok: true, data: M } } as any);
      await vm.load('s1');
      vm.setTab('physiological');
      await Promise.resolve(); await Promise.resolve();
      vm.startEditBodyMeasurement();
      await vm.saveBodyMeasurement();
      expect(createBodyMeasurementUc.run).toHaveBeenCalled();
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.bodyMeasurementSaved');
      expect(vm.editingBodyMeasurement()).toBe(false);
      expect(getBodyMeasurementUc.run).toHaveBeenCalledTimes(2); // initial tab open + reload after save
    });
  });

  describe('records tab', () => {
    const R = (id: string, cat: string, date: string) => ({ id, swimmerId: 's1', categoryId: cat, fieldLabel: 'L', value: 'V', observedDate: date });

    it('setTab("records") lazy-loads records + categories once and groups newest-first', async () => {
      const { vm, listRecordsUc, loadObsCatsUc } = build();
      await vm.load('s1');
      vm.setTab('records');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('records');
      expect(listRecordsUc.run).toHaveBeenCalledTimes(1);
      expect(loadObsCatsUc.run).toHaveBeenCalledTimes(1);
      const groups = vm.recordGroups();
      expect(groups).toHaveLength(2);                        // c1 + c2
      expect(groups[0].categoryId).toBe('c1');              // seeded order
      expect(groups[0].rows.map((r) => r.id)).toEqual(['o2', 'o1']); // newest first
      vm.setTab('identityVitals');
      vm.setTab('records');
      await Promise.resolve();
      expect(listRecordsUc.run).toHaveBeenCalledTimes(1);   // not reloaded
    });

    it('canSaveRecord requires category + label + value', async () => {
      const { vm } = build();
      await vm.load('s1');
      vm.startEditRecord(R('o1', 'c1', '2026-09-10T10:00:00Z'));
      expect(vm.canSaveRecord()).toBe(true);
      vm.recValue.set('');
      expect(vm.canSaveRecord()).toBe(false);
    });

    it('saveRecord updates, toasts and reloads', async () => {
      const { vm, updateRecordUc, listRecordsUc, notify } = build();
      await vm.load('s1');
      vm.setTab('records');
      await Promise.resolve(); await Promise.resolve();
      vm.startEditRecord(R('o1', 'c1', '2026-09-10T10:00:00Z'));
      vm.recLabel.set('Penicillin'); vm.recValue.set('Moderate');
      await vm.saveRecord();
      expect(updateRecordUc.run).toHaveBeenCalledWith({ recordId: 'o1', rq: { categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Moderate' } });
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.recordSaved');
      expect(vm.editingRecordId()).toBeNull();
      expect(listRecordsUc.run).toHaveBeenCalledTimes(2);
    });

    it('delete flow confirms, deletes, toasts and reloads', async () => {
      const { vm, deleteRecordUc, listRecordsUc, notify } = build();
      await vm.load('s1');
      vm.setTab('records');
      await Promise.resolve(); await Promise.resolve();
      vm.askDeleteRecord('o1');
      expect(vm.confirmingRecordDeleteId()).toBe('o1');
      await vm.confirmDeleteRecord();
      expect(deleteRecordUc.run).toHaveBeenCalledWith({ recordId: 'o1' });
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.recordRemoved');
      expect(vm.confirmingRecordDeleteId()).toBeNull();
      expect(listRecordsUc.run).toHaveBeenCalledTimes(2);
    });
  });

  describe('inbody tab', () => {
    it('setTab("inbody") lazy-loads readings once and selects the latest', async () => {
      const { vm, listInBodyUc } = build();
      await vm.load('s1');
      vm.setTab('inbody');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('inbody');
      expect(vm.inbodyReadings()).toHaveLength(2);
      expect(vm.selectedInBodyId()).toBe('r2');            // newest first
      expect(vm.selectedInBodyReading()?.id).toBe('r2');
      expect(listInBodyUc.run).toHaveBeenCalledTimes(1);
      vm.setTab('identityVitals');
      vm.setTab('inbody');
      await Promise.resolve();
      expect(listInBodyUc.run).toHaveBeenCalledTimes(1);   // not reloaded
    });

    it('inbodyHistory builds a metric matrix with latest-vs-previous change', async () => {
      const { vm } = build();
      await vm.load('s1');
      vm.setTab('inbody');
      await Promise.resolve(); await Promise.resolve();
      const h = vm.inbodyHistory();
      expect(h.dates).toEqual(['2024-06-15', '2024-10-04']);  // oldest -> newest
      const weight = h.rows.find((r) => r.labelKey === 'swimmerProfile.inbody.weight')!;
      expect(weight.values).toEqual([72.5, 74.0]);
      expect(weight.change).toBe(1.5);                        // 74.0 - 72.5
    });

    it('canSaveInBody enforces date + metric ranges', async () => {
      const { vm } = build();
      await vm.load('s1');
      vm.startAddInBody();
      vm.ibDate.set('');   // startAddInBody sets today; clear it
      expect(vm.canSaveInBody()).toBe(false);
      vm.ibDate.set('2024-10-04');
      vm.ibHeight.set('180'); vm.ibWeight.set('74'); vm.ibFat.set('12.8'); vm.ibMuscle.set('42.1'); vm.ibWater.set('55'); vm.ibBone.set('1.35'); vm.ibBody.set('1.07');
      expect(vm.canSaveInBody()).toBe(true);
      vm.ibFat.set('101');   // out of 0..100
      expect(vm.canSaveInBody()).toBe(false);
    });

    it('saveInBody creates, toasts and reloads; delete flow confirms and reloads', async () => {
      const { vm, createInBodyUc, deleteInBodyUc, listInBodyUc, notify } = build();
      await vm.load('s1');
      vm.setTab('inbody');
      await Promise.resolve(); await Promise.resolve();
      vm.startAddInBody();
      vm.ibHeight.set('181'); vm.ibWeight.set('74'); vm.ibFat.set('12.8'); vm.ibMuscle.set('42.1'); vm.ibWater.set('55'); vm.ibBone.set('1.35'); vm.ibBody.set('1.07');
      await vm.saveInBody();
      expect(createInBodyUc.run).toHaveBeenCalled();
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.readingSaved');
      expect(listInBodyUc.run).toHaveBeenCalledTimes(2);     // load + reload after save

      vm.askDeleteInBody();
      expect(vm.confirmingInBodyDelete()).toBe(true);
      await vm.confirmDeleteInBody();
      expect(deleteInBodyUc.run).toHaveBeenCalled();
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.readingDeleted');
      expect(listInBodyUc.run).toHaveBeenCalledTimes(3);     // reload after delete
    });
  });
});

describe('SwimmerProfileViewModel — Health Monitoring', () => {
  it('setTab loads health readings once (lazy)', async () => {
    const { vm, listHealthReadingsUc } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    expect(listHealthReadingsUc.run).toHaveBeenCalledTimes(1);
    vm.setTab('inbody');
    vm.setTab('healthMonitoring');
    expect(listHealthReadingsUc.run).toHaveBeenCalledTimes(1); // not reloaded
    expect(vm.healthReadings()).toHaveLength(2);
  });

  it('healthReadingRows filters by From/To (inclusive)', async () => {
    const { vm } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    vm.hmFrom.set('2023-10-01');
    expect(vm.healthReadingRows().map((r) => r.id)).toEqual(['h1']); // h2 is 2023-07-15, excluded
    vm.hmFrom.set('');
    vm.hmTo.set('2023-08-01');
    expect(vm.healthReadingRows().map((r) => r.id)).toEqual(['h2']);
  });

  it('canSaveHealthReading requires a positive number', async () => {
    const { vm } = build();
    await vm.load('s1');
    vm.hrValue.set('0'); expect(vm.canSaveHealthReading()).toBe(false);
    vm.hrValue.set('abc'); expect(vm.canSaveHealthReading()).toBe(false);
    vm.hrValue.set('12.3'); expect(vm.canSaveHealthReading()).toBe(true);
  });

  it('saveHealthReading updates, toasts, and reloads', async () => {
    const { vm, updateHealthReadingUc, listHealthReadingsUc, notify } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    vm.startEditHealthReading({ id: 'h2', medicalTestId: 't2', testNameEn: 'Ferritin', testNameAr: 'فيريتين', unit: 'ng/mL', value: 22, lowerBound: 30, upperBound: 400, readingDate: '2023-07-15T00:00:00Z', status: 'out' });
    vm.hrValue.set('35');
    await vm.saveHealthReading();
    expect(updateHealthReadingUc.run).toHaveBeenCalledWith({ id: 'h2', rq: { value: 35 } });
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.healthReadingUpdated');
    expect(listHealthReadingsUc.run).toHaveBeenCalledTimes(2); // initial + reload
    expect(vm.editingHealthReadingId()).toBeNull();
  });

  it('confirmDeleteHealthReading deletes, toasts, and reloads', async () => {
    const { vm, deleteHealthReadingUc, notify } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    vm.askDeleteHealthReading('h1');
    await vm.confirmDeleteHealthReading();
    expect(deleteHealthReadingUc.run).toHaveBeenCalledWith({ id: 'h1' });
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.healthReadingRemoved');
    expect(vm.confirmingHealthReadingDeleteId()).toBeNull();
  });
});

describe('SwimmerProfileViewModel — Feedback', () => {
  it('loads feedback + categories on setTab("feedback") and filters by date range', async () => {
    const { vm } = build({
      listFeedback: { ok: true, data: [
        { id: 'f1', rating: 5, categoryId: 'c1', comment: 'a', authorNameEn: 'Coach', authorNameAr: null, entryDate: '2024-10-22' },
        { id: 'f2', rating: 3, categoryId: 'c1', comment: 'b', authorNameEn: 'Coach', authorNameAr: null, entryDate: '2024-09-01' },
      ] },
      feedbackCategories: { ok: true, data: [{ id: 'c1', code: 'technique', nameEn: 'Technique', nameAr: null }] },
    });
    await vm.load('s1');
    vm.setTab('feedback');
    await Promise.resolve(); await Promise.resolve();

    expect(vm.feedbackEntries().length).toBe(2);
    vm.fbFrom.set('2024-10-01');
    expect(vm.feedbackRows().length).toBe(1); // only f1 is in range
  });
});

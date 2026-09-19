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
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const IDENTITY = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: 'أحمد', dob: '2010-01-01', age: 16, genderCode: 'male', phone: '01000000001', trainingClubNameEn: 'Oasis', trainingClubNameAr: null };
const VITALS = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 14.8, heightCm: 182, weightKg: 74, internalMed: REF, heartAssess: REF, spineAssess: REF };
const VITALS2 = { id: 'e2', examDate: '2024-01-01', bloodType: null, hemoglobin: 13, heightCm: 178, weightKg: 71, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(over: { profile?: unknown; update?: unknown; create?: unknown; list?: unknown; updateExam?: unknown; deleteExam?: unknown; role?: 'head_coach' | 'captain' | null } = {}) {
  const getUc = { run: jest.fn().mockResolvedValue(over.profile ?? { ok: true, data: { identity: IDENTITY, vitals: VITALS } }) };
  const updateUc = { run: jest.fn().mockResolvedValue(over.update ?? { ok: true, data: undefined }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: VITALS }) };
  const listExamsUc = { run: jest.fn().mockResolvedValue(over.list ?? { ok: true, data: [VITALS, VITALS2] }) };
  const updateExamUc = { run: jest.fn().mockResolvedValue(over.updateExam ?? { ok: true, data: VITALS }) };
  const deleteExamUc = { run: jest.fn().mockResolvedValue(over.deleteExam ?? { ok: true, data: undefined }) };
  const bloodUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const fitnessUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [REF] }) };
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
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
    { provide: AuthSessionStore, useValue: session },
  ] });
  return { vm: TestBed.inject(SwimmerProfileViewModel), getUc, updateUc, createUc, listExamsUc, updateExamUc, deleteExamUc, notify };
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
});

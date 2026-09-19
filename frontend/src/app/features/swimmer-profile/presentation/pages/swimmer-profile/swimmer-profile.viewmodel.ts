import { Injectable, computed, inject, signal } from '@angular/core';
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
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';
import { LookupItem } from '@features/reference/domain/model/reference';
import { BodyMeasurement } from '@features/swimmer-profile/domain/model/body-measurement';
import { SwimmerProfile, SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';
import { SwimmerGuardians } from '@features/swimmer-profile/domain/model/swimmer-guardians';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

@Injectable()
export class SwimmerProfileViewModel {
  private readonly getProfileUc = inject(GetSwimmerProfileUseCase);
  private readonly updateIdentityUc = inject(UpdateSwimmerIdentityUseCase);
  private readonly createExamUc = inject(CreateMedicalExamUseCase);
  private readonly listExamsUc = inject(ListMedicalExamsUseCase);
  private readonly updateExamUc = inject(UpdateMedicalExamUseCase);
  private readonly deleteExamUc = inject(DeleteMedicalExamUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadFitness = inject(LoadFitnessAssessmentsUseCase);
  private readonly getGuardiansUc = inject(GetSwimmerGuardiansUseCase);
  private readonly upsertGuardiansUc = inject(UpsertSwimmerGuardiansUseCase);
  private readonly getBodyMeasurementUc = inject(GetLatestBodyMeasurementUseCase);
  private readonly createBodyMeasurementUc = inject(CreateBodyMeasurementUseCase);
  private readonly listInBodyUc = inject(ListInBodyReadingsUseCase);
  private readonly createInBodyUc = inject(CreateInBodyReadingUseCase);
  private readonly updateInBodyUc = inject(UpdateInBodyReadingUseCase);
  private readonly deleteInBodyUc = inject(DeleteInBodyReadingUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);
  private readonly session = inject(AuthSessionStore);

  readonly profile = signal<SwimmerProfile | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly notFound = signal(false);

  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly fitnessAssessments = signal<LookupItem[]>([]);

  readonly canEdit = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  // Identity edit state
  readonly editingIdentity = signal(false);
  readonly idNameEn = signal('');
  readonly idNameAr = signal('');
  readonly idDob = signal('');
  readonly idPhone = signal('');
  readonly savingIdentity = signal(false);
  readonly canSaveIdentity = computed(() => this.idNameEn().trim().length > 0 && this.idDob().length > 0);

  // Vitals edit state (numerics held as strings from inputs)
  readonly editingVitals = signal(false);
  readonly vExamDate = signal('');
  readonly vBloodTypeId = signal('');
  readonly vHemoglobin = signal('');
  readonly vHeightCm = signal('');
  readonly vWeightKg = signal('');
  readonly vInternalMedId = signal('');
  readonly vHeartAssessId = signal('');
  readonly vSpineAssessId = signal('');
  readonly savingVitals = signal(false);
  readonly canSaveVitals = computed(() => {
    const nums = [this.vHemoglobin(), this.vHeightCm(), this.vWeightKg()];
    const numericOk = nums.every((v) => v.trim().length > 0 && Number.isFinite(Number(v)) && Number(v) > 0);
    return this.vExamDate().length > 0
      && this.vInternalMedId().length > 0 && this.vHeartAssessId().length > 0 && this.vSpineAssessId().length > 0
      && numericOk;
  });

  // Exam list state
  readonly exams = signal<SwimmerVitals[]>([]);
  readonly selectedExamId = signal('');
  readonly loadingExams = signal(false);
  readonly selectedExam = computed(() =>
    this.exams().find((e) => e.id === this.selectedExamId()) ?? this.exams()[0] ?? null);
  readonly editingExamId = signal<string | null>(null);
  readonly confirmingDelete = signal(false);
  readonly deleting = signal(false);

  // Tab state — only the two built tabs are switchable.
  readonly activeTab = signal<'identityVitals' | 'guardian' | 'physiological' | 'inbody'>('identityVitals');
  private guardiansLoaded = false;
  private bodyMeasurementLoaded = false;
  private inbodyLoaded = false;

  // Guardian state
  readonly guardians = signal<SwimmerGuardians | null>(null);
  readonly loadingGuardians = signal(false);
  readonly editingGuardians = signal(false);
  readonly savingGuardians = signal(false);
  readonly gFatherName = signal(''); readonly gFatherNationalId = signal(''); readonly gFatherPhone = signal('');
  readonly gMotherName = signal(''); readonly gMotherNationalId = signal(''); readonly gMotherPhone = signal('');

  // Body measurement (physiological) state
  readonly bodyMeasurement = signal<BodyMeasurement | null>(null);
  readonly loadingBodyMeasurement = signal(false);
  readonly editingBodyMeasurement = signal(false);
  readonly savingBodyMeasurement = signal(false);
  readonly bmRightArm = signal(''); readonly bmLeftArm = signal(''); readonly bmRightLeg = signal(''); readonly bmLeftLeg = signal('');
  readonly bmTorso = signal(''); readonly bmBustDiameter = signal(''); readonly bmWaistDiameter = signal('');

  readonly canSaveBodyMeasurement = computed(() => {
    const nums = [this.bmRightArm(), this.bmLeftArm(), this.bmRightLeg(), this.bmLeftLeg(), this.bmTorso(), this.bmBustDiameter(), this.bmWaistDiameter()];
    return nums.every((v) => {
      const n = Number(v);
      return v.trim().length > 0 && Number.isFinite(n) && n > 0 && n <= 999.9;
    });
  });

  // InBody state
  readonly inbodyReadings = signal<InBodyReading[]>([]);
  readonly selectedInBodyId = signal('');
  readonly loadingInBody = signal(false);
  readonly editingInBody = signal(false);
  readonly editingInBodyId = signal<string | null>(null);
  readonly savingInBody = signal(false);
  readonly confirmingInBodyDelete = signal(false);
  readonly deletingInBody = signal(false);
  readonly ibDate = signal(''); readonly ibHeight = signal(''); readonly ibWeight = signal('');
  readonly ibFat = signal(''); readonly ibMuscle = signal(''); readonly ibWater = signal(''); readonly ibBone = signal(''); readonly ibBody = signal('');

  readonly selectedInBodyReading = computed(() =>
    this.inbodyReadings().find((r) => r.id === this.selectedInBodyId()) ?? this.inbodyReadings()[0] ?? null);

  readonly canSaveInBody = computed(() => {
    const num = (v: string) => (v.trim().length > 0 && Number.isFinite(Number(v)) ? Number(v) : NaN);
    const h = num(this.ibHeight()), w = num(this.ibWeight()), f = num(this.ibFat()),
          mu = num(this.ibMuscle()), wa = num(this.ibWater()), bo = num(this.ibBone()), bd = num(this.ibBody());
    return this.ibDate().length > 0
      && h > 0 && h <= 999.9 && w > 0 && w <= 999.9
      && f >= 0 && f <= 100 && mu >= 0 && mu <= 100 && wa >= 0 && wa <= 100
      && bo > 0 && bo <= 99.99 && bd > 0 && bd <= 99.99;
  });

  readonly inbodyHistory = computed(() => {
    const readings = [...this.inbodyReadings()].sort((a, b) => a.readingDate.localeCompare(b.readingDate)); // oldest -> newest
    const metrics = [
      { key: 'weightKg' as const, labelKey: 'swimmerProfile.inbody.weight', unit: 'kg' },
      { key: 'fatPct' as const, labelKey: 'swimmerProfile.inbody.fatPercent', unit: '%' },
      { key: 'musclePct' as const, labelKey: 'swimmerProfile.inbody.musclePercent', unit: '%' },
      { key: 'waterPct' as const, labelKey: 'swimmerProfile.inbody.waterPercent', unit: '%' },
      { key: 'boneDensity' as const, labelKey: 'swimmerProfile.inbody.boneDensity', unit: '' },
      { key: 'bodyDensity' as const, labelKey: 'swimmerProfile.inbody.bodyDensity', unit: '' },
    ];
    return {
      dates: readings.map((r) => r.readingDate),
      rows: metrics.map((m) => {
        const values = readings.map((r) => r[m.key]);
        const n = values.length;
        const change = n >= 2 ? Math.round((values[n - 1] - values[n - 2]) * 100) / 100 : null;
        return { labelKey: m.labelKey, unit: m.unit, values, change };
      }),
    };
  });

  private static readonly NATIONAL_ID = /^\d{14}$/;
  readonly canSaveGuardians = computed(() => {
    const slotOk = (name: string, nid: string, phone: string) =>
      name.trim().length > 0 && SwimmerProfileViewModel.NATIONAL_ID.test(nid.trim()) && phone.trim().length > 0;
    return slotOk(this.gFatherName(), this.gFatherNationalId(), this.gFatherPhone())
        && slotOk(this.gMotherName(), this.gMotherNationalId(), this.gMotherPhone());
  });

  private swimmerId = '';

  async load(id: string): Promise<void> {
    this.swimmerId = id;
    // The view-model instance persists across swimmer navigations (route-level provider),
    // so reset per-swimmer tab + guardian state; otherwise the tab and stale guardian data
    // carry over to the next swimmer (and the guardian tab never re-fetches).
    this.activeTab.set('identityVitals');
    this.guardiansLoaded = false;
    this.guardians.set(null);
    this.editingGuardians.set(false);
    this.bodyMeasurementLoaded = false;
    this.bodyMeasurement.set(null);
    this.editingBodyMeasurement.set(false);
    this.inbodyLoaded = false;
    this.inbodyReadings.set([]);
    this.editingInBody.set(false);
    this.confirmingInBodyDelete.set(false);
    this.loading.set(true);
    this.error.set(false);
    this.notFound.set(false);
    const r = await this.getProfileUc.run(id);
    this.loading.set(false);
    if (r.ok) {
      this.profile.set(r.data);
      await this.loadExams();
    } else {
      this.profile.set(null);
      if (r.error.status === 404) this.notFound.set(true);
      else this.error.set(true);
    }
  }

  private async loadExams(): Promise<void> {
    this.loadingExams.set(true);
    const r = await this.listExamsUc.run(this.swimmerId);
    this.loadingExams.set(false);
    if (r.ok) {
      this.exams.set(r.data);
      this.selectedExamId.set(r.data[0]?.id ?? '');
    } else {
      this.exams.set([]);
      this.selectedExamId.set('');
    }
  }

  private async ensureLookups(): Promise<void> {
    if (this.bloodTypes().length === 0) {
      const b = await this.loadBloodTypes.run();
      if (b.ok) this.bloodTypes.set(b.data);
    }
    if (this.fitnessAssessments().length === 0) {
      const f = await this.loadFitness.run();
      if (f.ok) this.fitnessAssessments.set(f.data);
    }
  }

  startEditIdentity(): void {
    const i = this.profile()?.identity;
    if (!i) return;
    this.idNameEn.set(i.nameEn);
    this.idNameAr.set(i.nameAr ?? '');
    this.idDob.set(i.dob ?? '');
    this.idPhone.set(i.phone ?? '');
    this.editingIdentity.set(true);
  }

  cancelEditIdentity(): void { this.editingIdentity.set(false); }

  async saveIdentity(): Promise<void> {
    if (!this.canSaveIdentity() || this.savingIdentity()) return;
    this.savingIdentity.set(true);
    const r = await this.updateIdentityUc.run({
      id: this.swimmerId,
      rq: { nameEn: this.idNameEn().trim(), nameAr: this.idNameAr().trim() || null, dob: this.idDob(), phone: this.idPhone().trim() || null },
    });
    this.savingIdentity.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.identityUpdated'));
      this.editingIdentity.set(false);
      await this.load(this.swimmerId);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  async startNewExam(): Promise<void> {
    await this.ensureLookups();
    this.editingExamId.set(null);
    this.vExamDate.set(new Date().toISOString().slice(0, 10));
    this.vBloodTypeId.set('');
    this.vHemoglobin.set(''); this.vHeightCm.set(''); this.vWeightKg.set('');
    this.vInternalMedId.set(''); this.vHeartAssessId.set(''); this.vSpineAssessId.set('');
    this.editingVitals.set(true);
  }

  async startEditExam(): Promise<void> {
    const v = this.selectedExam();
    if (!v) return;
    await this.ensureLookups();
    this.editingExamId.set(v.id);
    this.vExamDate.set(v.examDate);
    this.vBloodTypeId.set(v.bloodType?.id ?? '');
    this.vHemoglobin.set(String(v.hemoglobin)); this.vHeightCm.set(String(v.heightCm)); this.vWeightKg.set(String(v.weightKg));
    this.vInternalMedId.set(v.internalMed.id); this.vHeartAssessId.set(v.heartAssess.id); this.vSpineAssessId.set(v.spineAssess.id);
    this.editingVitals.set(true);
  }

  cancelEditVitals(): void { this.editingVitals.set(false); this.editingExamId.set(null); }

  async saveVitals(): Promise<void> {
    if (!this.canSaveVitals() || this.savingVitals()) return;
    this.savingVitals.set(true);
    const rq = {
      examDate: this.vExamDate(),
      bloodTypeId: this.vBloodTypeId() || null,
      hemoglobin: Number(this.vHemoglobin()), heightCm: Number(this.vHeightCm()), weightKg: Number(this.vWeightKg()),
      internalMedId: this.vInternalMedId(), heartAssessId: this.vHeartAssessId(), spineAssessId: this.vSpineAssessId(),
    };
    const examId = this.editingExamId();
    const r = examId
      ? await this.updateExamUc.run({ id: this.swimmerId, examId, rq })
      : await this.createExamUc.run({ id: this.swimmerId, rq });
    this.savingVitals.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t(examId ? 'swimmerProfile.toasts.examUpdated' : 'swimmerProfile.toasts.examRecorded'));
      this.editingVitals.set(false);
      this.editingExamId.set(null);
      await this.loadExams();
      this.selectedExamId.set(r.data.id);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  askDeleteExam(): void { this.confirmingDelete.set(true); }
  cancelDelete(): void { this.confirmingDelete.set(false); }

  async confirmDeleteExam(): Promise<void> {
    const v = this.selectedExam();
    if (!v || this.deleting()) return;
    this.deleting.set(true);
    const r = await this.deleteExamUc.run({ id: this.swimmerId, examId: v.id });
    this.deleting.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.examDeleted'));
      this.confirmingDelete.set(false);
      await this.loadExams();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }

  setTab(key: 'identityVitals' | 'guardian' | 'physiological' | 'inbody'): void {
    this.activeTab.set(key);
    if (key === 'guardian' && !this.guardiansLoaded) void this.loadGuardians();
    if (key === 'physiological' && !this.bodyMeasurementLoaded) void this.loadBodyMeasurement();
    if (key === 'inbody' && !this.inbodyLoaded) void this.loadInBody();
  }

  private async loadGuardians(): Promise<void> {
    this.guardiansLoaded = true;
    this.loadingGuardians.set(true);
    const r = await this.getGuardiansUc.run(this.swimmerId);
    this.loadingGuardians.set(false);
    if (r.ok) this.guardians.set(r.data);
    else { this.guardiansLoaded = false; this.guardians.set(null); }
  }

  startEditGuardians(): void {
    const g = this.guardians();
    this.gFatherName.set(g?.father?.name ?? ''); this.gFatherNationalId.set(g?.father?.nationalId ?? ''); this.gFatherPhone.set(g?.father?.phone ?? '');
    this.gMotherName.set(g?.mother?.name ?? ''); this.gMotherNationalId.set(g?.mother?.nationalId ?? ''); this.gMotherPhone.set(g?.mother?.phone ?? '');
    this.editingGuardians.set(true);
  }

  cancelEditGuardians(): void { this.editingGuardians.set(false); }

  async saveGuardians(): Promise<void> {
    if (!this.canSaveGuardians() || this.savingGuardians()) return;
    this.savingGuardians.set(true);
    const rq = {
      father: { name: this.gFatherName().trim(), nationalId: this.gFatherNationalId().trim(), phone: this.gFatherPhone().trim() },
      mother: { name: this.gMotherName().trim(), nationalId: this.gMotherNationalId().trim(), phone: this.gMotherPhone().trim() },
    };
    const r = await this.upsertGuardiansUc.run({ id: this.swimmerId, rq });
    this.savingGuardians.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.guardiansSaved'));
      this.editingGuardians.set(false);
      this.guardiansLoaded = false;
      await this.loadGuardians();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  private async loadBodyMeasurement(): Promise<void> {
    this.bodyMeasurementLoaded = true;
    this.loadingBodyMeasurement.set(true);
    const r = await this.getBodyMeasurementUc.run(this.swimmerId);
    this.loadingBodyMeasurement.set(false);
    if (r.ok) this.bodyMeasurement.set(r.data);
    else { this.bodyMeasurementLoaded = false; this.bodyMeasurement.set(null); }
  }

  startEditBodyMeasurement(): void {
    const m = this.bodyMeasurement();
    this.bmRightArm.set(m ? String(m.rightArmCm) : '');
    this.bmLeftArm.set(m ? String(m.leftArmCm) : '');
    this.bmRightLeg.set(m ? String(m.rightLegCm) : '');
    this.bmLeftLeg.set(m ? String(m.leftLegCm) : '');
    this.bmTorso.set(m ? String(m.torsoCm) : '');
    this.bmBustDiameter.set(m ? String(m.bustDiameterCm) : '');
    this.bmWaistDiameter.set(m ? String(m.waistDiameterCm) : '');
    this.editingBodyMeasurement.set(true);
  }

  cancelEditBodyMeasurement(): void { this.editingBodyMeasurement.set(false); }

  async saveBodyMeasurement(): Promise<void> {
    if (!this.canSaveBodyMeasurement() || this.savingBodyMeasurement()) return;
    this.savingBodyMeasurement.set(true);
    const rq = {
      rightArmCm: Number(this.bmRightArm()), leftArmCm: Number(this.bmLeftArm()),
      rightLegCm: Number(this.bmRightLeg()), leftLegCm: Number(this.bmLeftLeg()),
      torsoCm: Number(this.bmTorso()), bustDiameterCm: Number(this.bmBustDiameter()), waistDiameterCm: Number(this.bmWaistDiameter()),
    };
    const r = await this.createBodyMeasurementUc.run({ id: this.swimmerId, rq });
    this.savingBodyMeasurement.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.bodyMeasurementSaved'));
      this.editingBodyMeasurement.set(false);
      this.bodyMeasurementLoaded = false;
      await this.loadBodyMeasurement();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  private async loadInBody(): Promise<void> {
    this.inbodyLoaded = true;
    this.loadingInBody.set(true);
    const r = await this.listInBodyUc.run(this.swimmerId);
    this.loadingInBody.set(false);
    if (r.ok) {
      this.inbodyReadings.set(r.data);
      this.selectedInBodyId.set(r.data[0]?.id ?? '');
    } else {
      this.inbodyLoaded = false;
      this.inbodyReadings.set([]);
      this.selectedInBodyId.set('');
    }
  }

  startAddInBody(): void {
    this.editingInBodyId.set(null);
    this.ibDate.set(new Date().toISOString().slice(0, 10));
    this.ibHeight.set(''); this.ibWeight.set(''); this.ibFat.set(''); this.ibMuscle.set(''); this.ibWater.set(''); this.ibBone.set(''); this.ibBody.set('');
    this.editingInBody.set(true);
  }

  startEditInBody(): void {
    const r = this.selectedInBodyReading();
    if (!r) return;
    this.editingInBodyId.set(r.id);
    this.ibDate.set(r.readingDate);
    this.ibHeight.set(String(r.heightCm)); this.ibWeight.set(String(r.weightKg));
    this.ibFat.set(String(r.fatPct)); this.ibMuscle.set(String(r.musclePct)); this.ibWater.set(String(r.waterPct));
    this.ibBone.set(String(r.boneDensity)); this.ibBody.set(String(r.bodyDensity));
    this.editingInBody.set(true);
  }

  cancelEditInBody(): void { this.editingInBody.set(false); this.editingInBodyId.set(null); }

  async saveInBody(): Promise<void> {
    if (!this.canSaveInBody() || this.savingInBody()) return;
    this.savingInBody.set(true);
    const rq = {
      readingDate: this.ibDate(),
      heightCm: Number(this.ibHeight()), weightKg: Number(this.ibWeight()),
      fatPct: Number(this.ibFat()), musclePct: Number(this.ibMuscle()), waterPct: Number(this.ibWater()),
      boneDensity: Number(this.ibBone()), bodyDensity: Number(this.ibBody()),
    };
    const readingId = this.editingInBodyId();
    const r = readingId
      ? await this.updateInBodyUc.run({ id: this.swimmerId, readingId, rq })
      : await this.createInBodyUc.run({ id: this.swimmerId, rq });
    this.savingInBody.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.readingSaved'));
      this.editingInBody.set(false);
      this.editingInBodyId.set(null);
      await this.loadInBody();
      this.selectedInBodyId.set(r.data.id);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  askDeleteInBody(): void { this.confirmingInBodyDelete.set(true); }
  cancelDeleteInBody(): void { this.confirmingInBodyDelete.set(false); }

  async confirmDeleteInBody(): Promise<void> {
    const r = this.selectedInBodyReading();
    if (!r || this.deletingInBody()) return;
    this.deletingInBody.set(true);
    const res = await this.deleteInBodyUc.run({ id: this.swimmerId, readingId: r.id });
    this.deletingInBody.set(false);
    if (res.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.readingDeleted'));
      this.confirmingInBodyDelete.set(false);
      await this.loadInBody();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }
}

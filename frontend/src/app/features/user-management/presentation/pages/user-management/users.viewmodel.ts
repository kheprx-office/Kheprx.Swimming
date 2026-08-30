import { Injectable, computed, inject, signal } from '@angular/core';
import { ListUsersUseCase } from '@features/user-management/domain/usecases/user-management/list-users.use-case';
import { CreateUserUseCase } from '@features/user-management/domain/usecases/user-management/create-user.use-case';
import { UpdateUserUseCase } from '@features/user-management/domain/usecases/user-management/update-user.use-case';
import { SetUserStatusUseCase } from '@features/user-management/domain/usecases/user-management/set-user-status.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { AppError } from '@core/domain/errors/app-error';
import { toUserMessage } from '@core/domain/errors/user-message';
import { User, UserStatus, Gender, CreateUserInput, UpdateUserInput } from '@features/user-management/domain/model/user-management/user';
import { UserRole } from '@core/domain/roles';
import { hasArabic } from '@core/text/arabic';

const NID_PATTERN = /^\d{14}$/;
const PHONE_PATTERN = /^01[0125]\d{8}$/;
const SEARCH_DEBOUNCE_MS = 300;

@Injectable()
export class UsersViewModel {
  private readonly listUsers = inject(ListUsersUseCase);
  private readonly createUser = inject(CreateUserUseCase);
  private readonly updateUser = inject(UpdateUserUseCase);
  private readonly setUserStatus = inject(SetUserStatusUseCase);
  private readonly notify = inject(NotificationService);

  readonly users = signal<User[]>([]);
  readonly loading = signal(false);
  readonly togglingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly search = signal('');
  readonly filterRole = signal<'' | UserRole>('');
  readonly filterStatus = signal<'' | UserStatus>('');

  readonly modalOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formFullName = signal('');
  readonly formEmail = signal('');
  readonly formRole = signal<UserRole>('manager');
  readonly formPassword = signal('');
  readonly formStatus = signal<UserStatus>('active');
  readonly formNid = signal('');
  readonly formPhone = signal('');
  readonly formGender = signal<'' | Gender>('');
  readonly formAge = signal('');
  readonly formMonthlySalary = signal('');
  readonly formDailyWage = signal('');
  readonly formCode = signal<string | null>(null);
  readonly formErrors = signal<Record<string, string>>({});

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  // search is server-side (name/nid); only the dropdown filters apply client-side
  readonly filtered = computed(() => {
    const fr = this.filterRole();
    const fs = this.filterStatus();
    return this.users().filter((u) => (!fr || u.role === fr) && (!fs || u.status === fs));
  });

  async init(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    const r = await this.listUsers.run(this.search().trim() || undefined);
    this.loading.set(false);
    if (r.ok) this.users.set(r.data);
    else this.notify.error(toUserMessage(r.error));
  }

  onSearchInput(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.searchTimer = null;
      void this.load();
    }, SEARCH_DEBOUNCE_MS);
  }

  openAdd(): void {
    this.editingId.set(null);
    this.resetForm();
    this.modalOpen.set(true);
  }

  openEdit(u: User): void {
    this.editingId.set(u.id);
    this.resetForm();
    this.formFullName.set(u.fullName);
    this.formEmail.set(u.email ?? '');
    this.formRole.set(u.role);
    this.formStatus.set(u.status);
    this.formNid.set(u.nid);
    this.formPhone.set(u.phone ?? '');
    this.formGender.set(u.gender ?? '');
    this.formAge.set(u.age === null ? '' : String(u.age));
    this.formCode.set(u.code);
    const p = u.profile;
    this.formMonthlySalary.set(p?.monthlySalary == null ? '' : String(p.monthlySalary));
    this.formDailyWage.set(p?.dailyWage == null ? '' : String(p.dailyWage));
    this.modalOpen.set(true);
  }

  closeModal(): void { this.modalOpen.set(false); }

  async save(): Promise<void> {
    if (!this.validateForm()) return;
    this.saving.set(true);
    try {
      const id = this.editingId();
      if (id) {
        const input: UpdateUserInput = { ...this.buildInput(), status: this.formStatus() };
        const r = await this.updateUser.run({ id, input });
        if (!r.ok) { this.handleSaveError(r.error); return; }
        this.notify.success('تم تحديث المستخدم');
      } else {
        const r = await this.createUser.run(this.buildInput());
        if (!r.ok) { this.handleSaveError(r.error); return; }
        this.notify.success('تمت إضافة المستخدم');
      }
      this.modalOpen.set(false);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  async toggleStatus(u: User): Promise<void> {
    const next: UserStatus = u.status === 'active' ? 'disabled' : 'active';
    this.togglingId.set(u.id);
    try {
      const r = await this.setUserStatus.run({ id: u.id, status: next });
      if (!r.ok) { this.notify.error(toUserMessage(r.error)); return; }
      this.notify.success(next === 'active' ? 'تم التفعيل' : 'تم التعطيل');
      await this.load();
    } finally {
      this.togglingId.set(null);
    }
  }

  private resetForm(): void {
    this.formFullName.set(''); this.formEmail.set(''); this.formRole.set('manager');
    this.formPassword.set(''); this.formStatus.set('active');
    this.formNid.set(''); this.formPhone.set(''); this.formGender.set(''); this.formAge.set('');
    this.formMonthlySalary.set(''); this.formDailyWage.set('');
    this.formCode.set(null); this.formErrors.set({});
  }

  // Mirrors the backend rules (nid 14 digits, age 14–90, email+password required on
  // create, password ≥8, role-conditional profile fields).
  private validateForm(): boolean {
    const errors: Record<string, string> = {};
    const isCreate = !this.editingId();
    if (!this.formFullName().trim()) errors['fullName'] = 'الاسم مطلوب';
    if (!NID_PATTERN.test(this.formNid().trim())) errors['nid'] = 'الرقم القومي يجب أن يتكون من 14 رقمًا';
    const email = this.formEmail().trim();
    const password = this.formPassword();
    if (isCreate && email === '') errors['email'] = 'البريد الإلكتروني مطلوب';
    if (isCreate && password === '') {
      errors['password'] = 'كلمة المرور مطلوبة';
    } else if (password && password.length < 8) {
      errors['password'] = 'كلمة المرور يجب ألا تقل عن 8 أحرف';
    }
    const phone = this.formPhone().trim();
    if (isCreate && phone === '') {
      errors['phone'] = 'رقم الهاتف مطلوب';
    } else if (phone !== '' && !PHONE_PATTERN.test(phone)) {
      errors['phone'] = 'رقم هاتف غير صالح';
    }

    if (isCreate && !this.formGender()) errors['gender'] = 'الجنس مطلوب';

    const ageStr = this.formAge().trim();
    if (isCreate && ageStr === '') {
      errors['age'] = 'العمر مطلوب';
    } else if (ageStr !== '') {
      const age = Number(ageStr);
      if (!Number.isInteger(age) || age < 14 || age > 90) errors['age'] = 'العمر يجب أن يكون بين 14 و90';
    }
    const role = this.formRole();
    if (role === 'manager') {
      const salary = Number(this.formMonthlySalary());
      if (this.formMonthlySalary().trim() === '' || !Number.isFinite(salary) || salary < 0) {
        errors['monthlySalary'] = 'الراتب الشهري غير صالح';
      }
    }
    if (role === 'moqawel' || role === 'worker') {
      const wage = Number(this.formDailyWage());
      if (this.formDailyWage().trim() === '' || !Number.isFinite(wage) || wage < 0) {
        errors['dailyWage'] = 'اليومية غير صالحة';
      }
    }
    // No Arabic allowed in these fields (Name is exempt).
    const guarded: Record<string, string> = { email, nid: this.formNid().trim(), phone, password };
    for (const [key, value] of Object.entries(guarded)) {
      if (hasArabic(value)) errors[key] = 'يجب الإدخال بالإنجليزية';
    }
    this.formErrors.set(errors);
    return Object.keys(errors).length === 0;
  }

  private buildInput(): CreateUserInput {
    return {
      fullName: this.formFullName().trim(),
      role: this.formRole(),
      nid: this.formNid().trim(),
      email: this.formEmail().trim() || null,
      password: this.formPassword() || null,
      phone: this.formPhone().trim() || null,
      gender: this.formGender() || null,
      age: this.formAge().trim() === '' ? null : Number(this.formAge()),
      monthlySalary: this.formMonthlySalary().trim() === '' ? null : Number(this.formMonthlySalary()),
      dailyWage: this.formDailyWage().trim() === '' ? null : Number(this.formDailyWage()),
    };
  }

  private handleSaveError(error: AppError): void {
    if (error.code === 'NID_IN_USE') {
      this.formErrors.update((e) => ({ ...e, nid: 'الرقم القومي مستخدم بالفعل' }));
    } else {
      this.notify.error(toUserMessage(error));
    }
  }
}

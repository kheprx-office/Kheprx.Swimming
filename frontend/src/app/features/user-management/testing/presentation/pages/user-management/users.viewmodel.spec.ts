import { TestBed } from '@angular/core/testing';
import { UsersViewModel } from '@features/user-management/presentation/pages/user-management/users.viewmodel';
import { ListUsersUseCase } from '@features/user-management/domain/usecases/user-management/list-users.use-case';
import { CreateUserUseCase } from '@features/user-management/domain/usecases/user-management/create-user.use-case';
import { UpdateUserUseCase } from '@features/user-management/domain/usecases/user-management/update-user.use-case';
import { SetUserStatusUseCase } from '@features/user-management/domain/usecases/user-management/set-user-status.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { GENERIC_ERROR_AR } from '@core/domain/errors/user-message';
import { User } from '@features/user-management/domain/model/user-management/user';

const admin: User = {
  id: 'USR-ADMIN', code: 'OWNER-1', fullName: 'admin (seed)', email: 'admin@kheprx.local',
  phone: null, gender: null, age: null, nid: '29801014500001',
  role: 'admin', status: 'active', mustChangePassword: true, profile: null,
};
const worker: User = {
  id: 'USR-WORKER', code: 'W-1', fullName: 'worker (seed)', email: 'worker@kheprx.local',
  phone: null, gender: null, age: null, nid: '29801014500002',
  role: 'worker', status: 'disabled', mustChangePassword: true,
  profile: { monthlySalary: null, dailyWage: 350 },
};
describe('UsersViewModel', () => {
  const list = { run: jest.fn() };
  const create = { run: jest.fn() };
  const update = { run: jest.fn() };
  const setStatus = { run: jest.fn() };
  const notify = { success: jest.fn(), error: jest.fn() };
  let vm: UsersViewModel;

  beforeEach(() => {
    jest.clearAllMocks();
    list.run.mockResolvedValue(ok([admin, worker]));
    TestBed.configureTestingModule({
      providers: [
        UsersViewModel,
        { provide: ListUsersUseCase, useValue: list },
        { provide: CreateUserUseCase, useValue: create },
        { provide: UpdateUserUseCase, useValue: update },
        { provide: SetUserStatusUseCase, useValue: setStatus },
        { provide: NotificationService, useValue: notify },
      ],
    });
    vm = TestBed.inject(UsersViewModel);
  });

  function fillValidWorkerForm(): void {
    vm.openAdd();
    vm.formFullName.set('New Worker');
    vm.formRole.set('worker');
    vm.formNid.set('29801014501234');
    vm.formEmail.set('new@kheprx.local');
    vm.formPassword.set('password8');
    vm.formDailyWage.set('350');
    vm.formPhone.set('01012345678');
    vm.formGender.set('male');
    vm.formAge.set('24');
  }

  it('init() loads users (no search)', async () => {
    await vm.init();
    expect(vm.users()).toHaveLength(2);
    expect(list.run).toHaveBeenCalledWith(undefined);
  });

  it('surfaces a load network failure as the generic error toast', async () => {
    list.run.mockResolvedValue(fail(new AppError('Http failure response for .../api/users: 0 Unknown Error', 'network')));
    await vm.load();
    expect(notify.error).toHaveBeenCalledWith(GENERIC_ERROR_AR);
  });

  it('onSearchInput debounces 300ms then reloads with the latest term', () => {
    jest.useFakeTimers();
    vm.onSearchInput('298');
    vm.onSearchInput('29801');
    expect(list.run).not.toHaveBeenCalled();
    jest.advanceTimersByTime(300);
    expect(list.run).toHaveBeenCalledTimes(1);
    expect(list.run).toHaveBeenCalledWith('29801');
    jest.useRealTimers();
  });

  it('filtered() applies only the role/status dropdowns (search is server-side)', async () => {
    await vm.init();
    vm.filterRole.set('admin');
    expect(vm.filtered().map((u) => u.id)).toEqual(['USR-ADMIN']);
    vm.filterRole.set('');
    vm.filterStatus.set('disabled');
    expect(vm.filtered().map((u) => u.id)).toEqual(['USR-WORKER']);
  });

  it('save() blocks with field errors: name, nid format, worker profile fields', async () => {
    vm.openAdd();
    vm.formRole.set('worker');
    vm.formNid.set('123');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['fullName']).toBe('الاسم مطلوب');
    expect(vm.formErrors()['nid']).toBe('الرقم القومي يجب أن يتكون من 14 رقمًا');
    expect(vm.formErrors()['dailyWage']).toBe('اليومية غير صالحة');
    expect(vm.modalOpen()).toBe(true);
  });

  it('save() requires a password on create', async () => {
    fillValidWorkerForm();
    vm.formPassword.set('');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['password']).toBe('كلمة المرور مطلوبة');
  });

  it('save() enforces password min length when present', async () => {
    fillValidWorkerForm();
    vm.formPassword.set('short');
    await vm.save();
    expect(vm.formErrors()['password']).toBe('كلمة المرور يجب ألا تقل عن 8 أحرف');
  });

  it('save() requires an email and flags a short password independently', async () => {
    fillValidWorkerForm();
    vm.formEmail.set('');
    vm.formPassword.set('short');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['email']).toBe('البريد الإلكتروني مطلوب');
    expect(vm.formErrors()['password']).toBe('كلمة المرور يجب ألا تقل عن 8 أحرف');
  });

  it('save() enforces manager monthly salary', async () => {
    vm.openAdd();
    vm.formFullName.set('M');
    vm.formRole.set('manager');
    vm.formNid.set('29801014501234');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['monthlySalary']).toBe('الراتب الشهري غير صالح');
  });

  it('save() enforces the age range when provided', async () => {
    fillValidWorkerForm();
    vm.formAge.set('9');
    await vm.save();
    expect(vm.formErrors()['age']).toBe('العمر يجب أن يكون بين 14 و90');
  });

  it('save() in add mode sends the built input then reloads + toasts', async () => {
    await vm.init();
    fillValidWorkerForm();
    create.run.mockResolvedValue(ok(worker));
    await vm.save();
    expect(create.run).toHaveBeenCalledWith({
      fullName: 'New Worker', role: 'worker', nid: '29801014501234',
      email: 'new@kheprx.local', password: 'password8', phone: '01012345678',
      gender: 'male', age: 24,
      monthlySalary: null, dailyWage: 350,
    });
    expect(notify.success).toHaveBeenCalled();
    expect(vm.modalOpen()).toBe(false);
    expect(list.run).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('save() blocks a login-less create (email and password required)', async () => {
    fillValidWorkerForm();
    vm.formEmail.set('');
    vm.formPassword.set('');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['email']).toBe('البريد الإلكتروني مطلوب');
    expect(vm.formErrors()['password']).toBe('كلمة المرور مطلوبة');
  });

  it('save() maps NID_IN_USE to a nid field error and keeps the modal open', async () => {
    fillValidWorkerForm();
    create.run.mockResolvedValue(fail(new AppError('الرقم القومي مستخدم بالفعل', 'http', 409, 'NID_IN_USE')));
    await vm.save();
    expect(vm.formErrors()['nid']).toBe('الرقم القومي مستخدم بالفعل');
    expect(notify.error).not.toHaveBeenCalled();
    expect(vm.modalOpen()).toBe(true);
  });

  it('save() surfaces other failures as an error toast and keeps the modal open', async () => {
    fillValidWorkerForm();
    create.run.mockResolvedValue(fail(new AppError('البريد الإلكتروني مستخدم بالفعل', 'http', 409, 'EMAIL_IN_USE', 'البريد الإلكتروني مستخدم بالفعل')));
    await vm.save();
    expect(notify.error).toHaveBeenCalledWith('البريد الإلكتروني مستخدم بالفعل');
    expect(vm.modalOpen()).toBe(true);
  });

  it('openEdit() populates the full form including profile fields and code', () => {
    vm.openEdit(worker);
    expect(vm.editingId()).toBe('USR-WORKER');
    expect(vm.formNid()).toBe('29801014500002');
    expect(vm.formCode()).toBe('W-1');
    expect(vm.formDailyWage()).toBe('350');
    expect(vm.formStatus()).toBe('disabled');
  });

  it('openEdit() then save() sends the update with role echo + status', async () => {
    await vm.init();
    vm.openEdit(worker);
    update.run.mockResolvedValue(ok(worker));
    vm.formFullName.set('Renamed');
    await vm.save();
    expect(update.run).toHaveBeenCalledWith({
      id: 'USR-WORKER',
      input: expect.objectContaining({
        fullName: 'Renamed', role: 'worker', nid: '29801014500002', status: 'disabled',
        dailyWage: 350,
      }),
    });
  });

  it('toggleStatus() flips active/disabled and reloads', async () => {
    await vm.init();
    setStatus.run.mockResolvedValue(ok({ ...admin, status: 'disabled' }));
    await vm.toggleStatus(admin);
    expect(setStatus.run).toHaveBeenCalledWith({ id: 'USR-ADMIN', status: 'disabled' });
  });

  it('toggleStatus() sets togglingId to the row id during the call, then clears it on success', async () => {
    await vm.init();
    let resolveStatus!: (v: unknown) => void;
    setStatus.run.mockReturnValue(new Promise((res) => { resolveStatus = res; }));
    const pending = vm.toggleStatus(admin);
    expect(vm.togglingId()).toBe('USR-ADMIN'); // set synchronously, before the API resolves
    resolveStatus(ok({ ...admin, status: 'disabled' }));
    await pending;
    expect(vm.togglingId()).toBeNull();
  });

  it('toggleStatus() clears togglingId even when the API fails', async () => {
    await vm.init();
    setStatus.run.mockResolvedValue(fail(new AppError('boom', 'http', 500, 'SERVER')));
    await vm.toggleStatus(admin);
    expect(vm.togglingId()).toBeNull();
    expect(notify.error).toHaveBeenCalledWith(GENERIC_ERROR_AR);
  });

  it('save() sets saving() true during an in-flight create, then clears it on success', async () => {
    await vm.init();
    fillValidWorkerForm();
    let resolveCreate!: (v: unknown) => void;
    create.run.mockReturnValue(new Promise((res) => { resolveCreate = res; }));
    const pending = vm.save();
    expect(vm.saving()).toBe(true); // set synchronously, before the API resolves
    resolveCreate(ok(worker));
    await pending;
    expect(vm.saving()).toBe(false);
  });

  it('save() clears saving() even when the create fails', async () => {
    fillValidWorkerForm();
    create.run.mockResolvedValue(fail(new AppError('boom', 'http', 500, 'SERVER')));
    await vm.save();
    expect(vm.saving()).toBe(false);
  });

  it('save() never sets saving() true when validation fails', async () => {
    vm.openAdd(); // empty form → validateForm() returns false
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.saving()).toBe(false);
  });

  it('save() requires phone, gender, and age on create', async () => {
    fillValidWorkerForm();
    vm.formPhone.set('');
    vm.formGender.set('');
    vm.formAge.set('');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['phone']).toBe('رقم الهاتف مطلوب');
    expect(vm.formErrors()['gender']).toBe('الجنس مطلوب');
    expect(vm.formErrors()['age']).toBe('العمر مطلوب');
  });

  it('save() rejects a malformed phone on create', async () => {
    fillValidWorkerForm();
    vm.formPhone.set('011');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['phone']).toBe('رقم هاتف غير صالح');
  });

  it('save() rejects a phone with an out-of-set prefix on create', async () => {
    fillValidWorkerForm();
    vm.formPhone.set('01312345678');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['phone']).toBe('رقم هاتف غير صالح');
  });

  it('save() in edit mode does not require phone/gender/age', async () => {
    await vm.init();
    vm.openEdit(worker);
    update.run.mockResolvedValue(ok(worker));
    await vm.save();
    expect(update.run).toHaveBeenCalled();
    expect(vm.formErrors()['phone']).toBeUndefined();
    expect(vm.formErrors()['gender']).toBeUndefined();
    expect(vm.formErrors()['age']).toBeUndefined();
  });

  it('save() rejects a malformed phone on edit when one is entered', async () => {
    await vm.init();
    vm.openEdit(worker);
    vm.formPhone.set('011');
    await vm.save();
    expect(update.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['phone']).toBe('رقم هاتف غير صالح');
  });

  it('save() blocks Arabic in a guarded field with an English-only error', async () => {
    fillValidWorkerForm();
    vm.formEmail.set('عربي@x.com');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['email']).toBe('يجب الإدخال بالإنجليزية');
  });

  it('save() flags Arabic in the password field', async () => {
    fillValidWorkerForm();
    vm.formPassword.set('كلمةsecret');
    await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['password']).toBe('يجب الإدخال بالإنجليزية');
  });

  it('save() allows an Arabic name (name is not guarded)', async () => {
    fillValidWorkerForm();
    vm.formFullName.set('أحمد محمود');
    create.run.mockResolvedValue(ok(worker));
    await vm.save();
    expect(create.run).toHaveBeenCalled();
    expect(vm.formErrors()['fullName']).toBeUndefined();
  });

  it('save() blocks Arabic on the edit path (not just create)', async () => {
    await vm.init();
    vm.openEdit(worker);
    vm.formEmail.set('عربي@x.com');
    await vm.save();
    expect(update.run).not.toHaveBeenCalled();
    expect(vm.formErrors()['email']).toBe('يجب الإدخال بالإنجليزية');
  });
});

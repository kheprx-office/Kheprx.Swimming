import { TestBed } from '@angular/core/testing';
import { DeleteMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/delete-medical-exam.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(DeleteMedicalExamUseCase);
}

describe('DeleteMedicalExamUseCase', () => {
  it('calls the repository with id + examId', async () => {
    const deleteExam = jest.fn().mockResolvedValue({ data: null });
    const uc = build({ deleteExam } as unknown as ISwimmerProfileRepository);
    const r = await uc.run({ id: 's1', examId: 'e1' });
    expect(r.ok).toBe(true);
    expect(deleteExam).toHaveBeenCalledWith('s1', 'e1');
  });
  it('fails when the repository throws', async () => {
    const uc = build({ deleteExam: jest.fn().mockRejectedValue(new Error('boom')) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run({ id: 's1', examId: 'e1' });
    expect(r.ok).toBe(false);
  });
});

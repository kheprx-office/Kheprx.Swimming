import { AppError } from '@core/domain/errors/app-error';
import { UseCase } from '@core/domain/usecase/use-case';

class Doubler extends UseCase<number, number> {
  constructor() { super('Doubler'); }
  protected async execute(input: number): Promise<number> {
    if (input < 0) throw new AppError('negative', 'validation');
    if (input === 999) throw new Error('boom'); // non-AppError
    return input * 2;
  }
}

describe('UseCase', () => {
  const uc = new Doubler();

  it('returns ok with the execute() result on the happy path', async () => {
    const r = await uc.run(21);
    expect(r).toEqual({ ok: true, data: 42 });
  });

  it('passes a thrown AppError through unchanged (kind preserved)', async () => {
    const r = await uc.run(-1);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });

  it('normalizes a non-AppError throw to kind=unknown', async () => {
    const r = await uc.run(999);
    expect(r.ok).toBe(false);
    if (!r.ok) {
      expect(r.error.kind).toBe('unknown');
      expect(r.error.message).toBe('Unexpected error');
    }
  });
});

// UseCase: the base every use case extends. Subclasses implement only the happy
// path in execute(); run() owns the shared error handling once, so no use case
// repeats the same try/catch.
import { AppError } from '@core/domain/errors/app-error';
import { createLogger, Logger } from '@core/logging/logger';
import { Result, fail, ok } from '@core/domain/result/result';

export abstract class UseCase<I, O> {
  protected readonly log: Logger;

  constructor(name: string) {
    this.log = createLogger('usecase', name);
  }

  // happy path only — throw an AppError to signal failure
  protected abstract execute(input: I): Promise<O>;

  async run(input: I): Promise<Result<O>> {
    this.log.debug('run()');
    try {
      return ok(await this.execute(input));
    } catch (e) {
      const error =
        e instanceof AppError ? e : new AppError('Unexpected error', 'unknown');
      this.log.warn('failed:', error.kind, error.message);
      return fail(error);
    }
  }
}

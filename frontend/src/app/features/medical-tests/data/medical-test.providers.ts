import { Provider } from '@angular/core';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';
import { MedicalTestRepositoryImpl } from '@features/medical-tests/data/repositories/medical-test.repository.impl';

// Live wiring: bind the medical-tests repository port to the HTTP impl (/api/medical-tests).
export const MEDICAL_TEST_PROVIDERS: Provider[] = [
  { provide: MEDICAL_TEST_REPOSITORY, useClass: MedicalTestRepositoryImpl },
];

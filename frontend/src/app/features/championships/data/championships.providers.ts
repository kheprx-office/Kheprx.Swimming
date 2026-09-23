import { Provider } from '@angular/core';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { ChampionshipsRepositoryImpl } from '@features/championships/data/repositories/championships.repository.impl';

export const CHAMPIONSHIPS_PROVIDERS: Provider[] = [
  { provide: CHAMPIONSHIPS_REPOSITORY, useClass: ChampionshipsRepositoryImpl },
];

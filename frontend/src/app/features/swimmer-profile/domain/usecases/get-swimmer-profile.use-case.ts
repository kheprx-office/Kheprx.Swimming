import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { SwimmerProfileDtoRs, isSwimmerProfileDtoRsValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { SwimmerProfile } from '@features/swimmer-profile/domain/model/swimmer-profile';

@Injectable({ providedIn: 'root' })
export class GetSwimmerProfileUseCase extends UseCase<string, SwimmerProfile> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetSwimmerProfile'); }

  protected async execute(id: string): Promise<SwimmerProfile> {
    const res = await this.repo.getProfile(id);
    if (!isSwimmerProfileDtoRsValid(res.data)) throw new AppError('Invalid swimmer profile received', 'validation');
    return toProfile(res.data);
  }
}

function toProfile(d: SwimmerProfileDtoRs): SwimmerProfile {
  return {
    identity: {
      id: d.identity.id, uid: d.identity.uid, nameEn: d.identity.nameEn, nameAr: d.identity.nameAr ?? null,
      dob: d.identity.dob ?? null, age: typeof d.identity.age === 'number' ? d.identity.age : null,
      genderCode: d.identity.genderCode, phone: d.identity.phone ?? null,
      trainingClubNameEn: d.identity.trainingClubNameEn ?? null, trainingClubNameAr: d.identity.trainingClubNameAr ?? null,
    },
    vitals: d.vitals ? toVitals(d.vitals) : null,
  };
}

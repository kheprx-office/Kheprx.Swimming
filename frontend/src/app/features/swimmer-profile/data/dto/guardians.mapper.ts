import { GuardianDtoRs, SwimmerGuardiansDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';
import { Guardian, SwimmerGuardians } from '@features/swimmer-profile/domain/model/swimmer-guardians';

function toGuardian(d: GuardianDtoRs): Guardian {
  return { id: d.id, relationCode: d.relationCode, name: d.name, nationalId: d.nationalId, phone: d.phone };
}

export function toSwimmerGuardians(d: SwimmerGuardiansDtoRs): SwimmerGuardians {
  return { father: d.father ? toGuardian(d.father) : null, mother: d.mother ? toGuardian(d.mother) : null };
}

// vitals.mapper.ts — DTO → domain mapping shared by the profile/list/create/update use-cases.
import { CodedRefDtoRs, SwimmerVitalsDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';
import { LookupItem } from '@features/reference/domain/model/reference';

export function toRef(r: CodedRefDtoRs): LookupItem {
  return { id: r.id, code: r.code, nameEn: r.nameEn, nameAr: r.nameAr };
}

export function toVitals(d: SwimmerVitalsDtoRs): SwimmerVitals {
  return {
    id: d.id,
    examDate: d.examDate,
    bloodType: d.bloodType ? toRef(d.bloodType) : null,
    hemoglobin: d.hemoglobin, heightCm: d.heightCm, weightKg: d.weightKg,
    internalMed: toRef(d.internalMed), heartAssess: toRef(d.heartAssess), spineAssess: toRef(d.spineAssess),
  };
}

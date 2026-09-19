import { toSwimmerGuardians } from '@features/swimmer-profile/data/dto/guardians.mapper';
import { SwimmerGuardiansDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';

describe('toSwimmerGuardians', () => {
  it('maps both slots', () => {
    const dto: SwimmerGuardiansDtoRs = {
      father: { id: 'f1', relationCode: 'father', name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: { id: 'm1', relationCode: 'mother', name: 'Fatima Ibrahim', nationalId: '27505050123456', phone: '+201005554444' },
    };
    const g = toSwimmerGuardians(dto);
    expect(g.father?.name).toBe('Hassan Ali');
    expect(g.mother?.nationalId).toBe('27505050123456');
  });

  it('keeps null slots null', () => {
    const dto: SwimmerGuardiansDtoRs = { father: null, mother: null };
    const g = toSwimmerGuardians(dto);
    expect(g.father).toBeNull();
    expect(g.mother).toBeNull();
  });
});

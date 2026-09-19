import { BodyMeasurementDtoRs, SwimmerBodyMeasurementDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';
import { BodyMeasurement } from '@features/swimmer-profile/domain/model/body-measurement';

function toBodyMeasurement(d: BodyMeasurementDtoRs): BodyMeasurement {
  return {
    id: d.id,
    measuredAt: d.measuredAt,
    rightArmCm: d.rightArmCm,
    leftArmCm: d.leftArmCm,
    rightLegCm: d.rightLegCm,
    leftLegCm: d.leftLegCm,
    torsoCm: d.torsoCm,
    bustDiameterCm: d.bustDiameterCm,
    waistDiameterCm: d.waistDiameterCm,
  };
}

export function toLatestBodyMeasurement(d: SwimmerBodyMeasurementDtoRs): BodyMeasurement | null {
  return d.latest ? toBodyMeasurement(d.latest) : null;
}

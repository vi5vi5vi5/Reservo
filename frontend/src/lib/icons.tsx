import {
  Accessibility, Armchair, Battery, Bike, BookOpen, Box, Cable, Camera, Car, Cpu, DoorOpen, FlaskConical, Gamepad2,
  Glasses, Headphones, Keyboard, Laptop, Lightbulb, Mic, Microscope, Monitor, Mouse, MousePointerClick, Package,
  PenLine, Plug, Presentation, Printer, Projector, Router, Server, Smartphone, Snowflake, Speaker, Sun, Tablet, Tv,
  Users, Video, Volume2, Wrench, type LucideIcon,
} from 'lucide-react';
import type { ResourceKind } from '../api/types';

/**
 * Иконки категорий и оснащения. Категорий может быть сотни (их заводит администратор),
 * поэтому иконка подбирается по ключевым словам в коде категории, а если ничего не подошло —
 * по виду ресурса. Так новая категория «drones» или «vr-headsets» сразу выглядит осмысленно.
 */

const byKeyword: [RegExp, LucideIcon][] = [
  [/meeting|negotiat|conference/, Users],
  [/class|lecture|audit|aud/, BookOpen],
  [/lab/, FlaskConical],
  [/cowork|seat|desk/, Armchair],
  [/3d|printer|print/, Printer],
  [/project/, Projector],
  [/laptop|notebook|macbook/, Laptop],
  [/camera|photo|video-cam/, Camera],
  [/vr|headset|glasses/, Glasses],
  [/micro(?!scope)|mic|audio-rec/, Mic],
  [/microscope/, Microscope],
  [/headphone/, Headphones],
  [/speaker|sound|audio/, Speaker],
  [/tablet|ipad/, Tablet],
  [/phone|smartphone/, Smartphone],
  [/monitor|display|screen/, Monitor],
  [/tv|television/, Tv],
  [/console|game/, Gamepad2],
  [/keyboard/, Keyboard],
  [/mouse/, Mouse],
  [/cable|adapter/, Cable],
  [/router|network|wifi/, Router],
  [/server/, Server],
  [/cpu|computer|pc|arduino|raspberry|board/, Cpu],
  [/battery|power/, Battery],
  [/charger|plug/, Plug],
  [/light|lamp/, Lightbulb],
  [/tool|drill|wrench/, Wrench],
  [/bike|scooter/, Bike],
  [/car/, Car],
  [/present|flipchart/, Presentation],
  [/room|hall/, DoorOpen],
];

const byKind: Record<ResourceKind, LucideIcon> = {
  Room: DoorOpen,
  Seat: Armchair,
  Equipment: Package,
};

export function categoryIcon(code: string | null | undefined, kind?: ResourceKind): LucideIcon {
  const c = (code ?? '').toLowerCase();
  for (const [re, icon] of byKeyword) if (re.test(c)) return icon;
  return kind ? byKind[kind] : Box;
}

const features: Record<string, LucideIcon> = {
  projector: Projector,
  whiteboard: PenLine,
  'interactive-board': MousePointerClick,
  'video-conference': Video,
  computers: Monitor,
  'audio-system': Volume2,
  'air-conditioning': Snowflake,
  accessible: Accessibility,
  window: Sun,
};

export function featureIcon(code: string): LucideIcon {
  return features[code] ?? categoryIcon(code);
}

/** Творительный падеж оснащения для фразы «переговорная с проектором». */
const instrumental: Record<string, string> = {
  projector: 'проектором',
  whiteboard: 'маркерной доской',
  'interactive-board': 'интерактивной доской',
  'video-conference': 'видеосвязью',
  computers: 'компьютерами',
  'audio-system': 'звуком',
  'air-conditioning': 'кондиционером',
  accessible: 'доступной средой',
  window: 'окном',
};

export function featureWith(code: string, name: string): string {
  return instrumental[code] ?? name.toLowerCase();
}

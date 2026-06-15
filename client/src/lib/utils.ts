import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

// shadcn/ui's class-merge helper: clsx resolves conditional classes, twMerge dedupes conflicting
// Tailwind utilities so the last one wins. The shadcn baseline every generated component imports.
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

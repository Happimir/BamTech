import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

interface Capability {
  title: string;
  description: string;
}

/** Public introduction to ACTS for visitors who are not signed in. */
@Component({
  selector: 'acts-landing-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, RouterLink],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.scss',
})
export class LandingPage {
  protected readonly capabilities: Capability[] = [
    {
      title: 'Find anyone',
      description:
        'Search the directory by the beginning of a name. People who share a name stay separate, each with their own record and ID.',
    },
    {
      title: 'See the whole career',
      description:
        'Current rank and duty, first service date, and every assignment newest first, including retirements and returns to active duty.',
    },
    {
      title: 'Records you can rely on',
      description:
        'Every change follows the career rules, is checked by the server and is logged, so the history always adds up.',
    },
  ];
}

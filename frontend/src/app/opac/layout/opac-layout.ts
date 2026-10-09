import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './header';
import { FooterComponent } from './footer';
import { ChatWidgetComponent } from '../components/chat-widget/chat-widget';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'opac-layout',
  imports: [RouterOutlet, HeaderComponent, FooterComponent, ChatWidgetComponent],
  template: `
    <div class="min-h-screen flex flex-col bg-gray-50 font-sans text-gray-800">
      <opac-header />
      <main class="flex-grow">
        <router-outlet />
      </main>
      <opac-footer />
      <opac-chat-widget />
    </div>
  `
})
export class OpacLayout {}

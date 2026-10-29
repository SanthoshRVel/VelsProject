/*
 * ============================================================
 *  DSRC-Inspired V2V — Vehicle B  (FINAL v5)
 *  ESP32 Arduino Core v3.x compatible
 *
 *  LED LOGIC (3-stage distance + speed + TTC):
 *
 *  GREEN  → safe:
 *    - No obstacle, no peer danger, TTC > WARNING_TTC_S,
 *      dist > WARNING_DIST_M, speeds equal/zero or rear slower
 *
 *  YELLOW → warning:
 *    - dist between WARNING_DIST_M (0.5m) and DANGER_DIST_M (0.25m), OR
 *    - TTC between CRITICAL_TTC_S and WARNING_TTC_S
 *
 *  RED    → danger:
 *    - Obstacle very close (dist < DANGER_DIST_M = 0.25m), OR
 *    - TTC <= CRITICAL_TTC_S and both moving, OR
 *    - Rear vehicle speed > front vehicle speed (closing), OR
 *    - Front vehicle brakes (RED both), OR
 *    - Rear vehicle brakes (RED rear only, GREEN front)
 *
 *  ROLE DETECTION:
 *    Assigned by compile-time flag. Vehicle B is FRONT by default.
 *    (Change ROLE_IS_FRONT to false if you swap physical positions.)
 *
 *  FIRST STEP: paste Vehicle A's MAC into PEER_MAC[] below.
 *
 *  WIRING (identical to Vehicle A):
 *   HC-SR04  TRIG  → GPIO 5
 *   HC-SR04  ECHO  → GPIO 18  (via 1kΩ + 2kΩ voltage divider)
 *   Speed POT wiper → GPIO 34  (3.3V rail only)
 *   Brake POT wiper → GPIO 35  (3.3V rail only)
 *   LED Green  → GPIO 21 → 330Ω → GND
 *   LED Yellow → GPIO 22 → 330Ω → GND
 *   LED Red    → GPIO 23 → 330Ω → GND
 *   Buzzer (+) → GPIO 4  | Buzzer (–) → GND
 * ============================================================
 */

#include <esp_now.h>
#include <WiFi.h>
#include <ArduinoJson.h>

// ── PINS ───────────────────────────────────────────────────
#define PIN_TRIG        5
#define PIN_ECHO       18
#define PIN_POT_SPEED  34
#define PIN_POT_BRAKE  35
#define PIN_LED_GREEN  21
#define PIN_LED_YELLOW 22
#define PIN_LED_RED    23
#define PIN_BUZZER      4

// ── PEER MAC — paste Vehicle A's MAC here ──────────────────
uint8_t PEER_MAC[] = {0xD4, 0xE9, 0xF4, 0x78, 0x4D, 0x20};

// ── ROLE: Vehicle B = FRONT by default ─────────────────────
#define ROLE_IS_FRONT true

// ── CONSTANTS ──────────────────────────────────────────────
#define BRAKE_THRESHOLD   3500
#define DANGER_DIST_M     0.25f   // < 25 cm → RED
#define WARNING_DIST_M    0.50f   // 25–50 cm → YELLOW
#define CRITICAL_TTC_S    1.5f
#define WARNING_TTC_S     3.0f
#define MIN_SPEED_KMH     2.0f
#define MAX_DIST_CM     400.0f
#define TX_INTERVAL_MS     50
#define PRINT_INTERVAL_MS 1000
#define PEER_TIMEOUT_MS  2000

/* Json Structure*/
// Vehicle data structure
struct VehicleData_json {
  float speedKmh;
  float distFront_m;
  bool brakeActive;
  bool obstacleDetected;
  float ttc_s;
  int steerAdvisory;
};

VehicleData_json localjson;
VehicleData_json peerjson;

// ── SHARED PACKET ──────────────────────────────────────────
typedef struct __attribute__((packed)) VehicleData {
  uint8_t  id;
  float    speedKmh;
  float    distFront_m;
  bool     brakeActive;
  bool     obstacleDetected;
  float    ttc_s;
  uint8_t  steerAdvisory;
  uint32_t ts;
} VehicleData;

VehicleData local, peer;
bool        peerFresh  = false;
uint32_t    lastTx     = 0;
uint32_t    lastPrint  = 0;
uint32_t    lastPeerRx = 0;

// ── SENSORS ────────────────────────────────────────────────
float readDistanceCm() {
  digitalWrite(PIN_TRIG, LOW);  delayMicroseconds(2);
  digitalWrite(PIN_TRIG, HIGH); delayMicroseconds(10);
  digitalWrite(PIN_TRIG, LOW);
  long dur = pulseIn(PIN_ECHO, HIGH, 25000);
  if (dur == 0) return MAX_DIST_CM;
  return constrain((dur * 0.0343f) / 2.0f, 2.0f, MAX_DIST_CM);
}

float readSpeedKmh() {
  return analogRead(PIN_POT_SPEED) * (120.0f / 4095.0f);
}

bool readBrake() {
  return analogRead(PIN_POT_BRAKE) > BRAKE_THRESHOLD;
}

// ── TTC ────────────────────────────────────────────────────
float computeTTC(float vSelf, float vPeer, float dSelf) {
  float vRel = (vSelf - vPeer) / 3.6f;
  if (vRel <= 0.0f) return 9999.0f;
  if (dSelf < 0.01f) return 0.01f;
  return dSelf / vRel;
}

// ── STEERING ───────────────────────────────────────────────
uint8_t calcSteer(float ttc) {
  if (ttc > WARNING_TTC_S) return 0;
  if (peerFresh && peer.steerAdvisory == 1) return 2;
  if (peerFresh && peer.steerAdvisory == 2) return 1;
  return 2; // Vehicle B defaults right
}

// ── 3-STAGE DANGER LOGIC ───────────────────────────────────
void computeDanger(float ttc, bool iAmFront,
                   bool &localDanger, bool &localWarning,
                   bool &peerShouldAlert) {
  localDanger     = false;
  localWarning    = false;
  peerShouldAlert = false;

  bool rearIsMe   = !iAmFront;
  bool rearFaster = false;

  if (peerFresh && rearIsMe) {
    rearFaster = (local.speedKmh > peer.speedKmh + 1.0f)
                 && (local.speedKmh > MIN_SPEED_KMH);
  } else if (peerFresh && iAmFront) {
    rearFaster = (peer.speedKmh > local.speedKmh + 1.0f)
                 && (peer.speedKmh > MIN_SPEED_KMH);
  }

  // Rule 1 – obstacle very close
  if (local.obstacleDetected) {
    localDanger = true; peerShouldAlert = true; return;
  }

  // Rule 2 – rear faster than front
  if (rearFaster) {
    localDanger = true; peerShouldAlert = true; return;
  }

  // Rule 3 – TTC critical
  bool ttcCritical = (ttc <= CRITICAL_TTC_S)
                  && (local.speedKmh > MIN_SPEED_KMH)
                  && peerFresh
                  && (peer.speedKmh > MIN_SPEED_KMH);
  if (ttcCritical) {
    localDanger = true; peerShouldAlert = true; return;
  }

  // Rule 4 – I am front and I brake → warn both
  if (iAmFront && local.brakeActive) {
    localDanger = true; peerShouldAlert = true; return;
  }

  // Rule 5 – I am rear and I brake → only I am in danger
  if (rearIsMe && local.brakeActive) {
    localDanger = true; peerShouldAlert = false; return;
  }

  // Rule 6 – peer (front) reported danger → I (rear) go red
  if (peerFresh && rearIsMe) {
    bool peerInDanger = peer.obstacleDetected
                     || peer.brakeActive
                     || (peer.ttc_s <= CRITICAL_TTC_S
                         && peer.speedKmh > MIN_SPEED_KMH);
    if (peerInDanger) {
      localDanger = true; peerShouldAlert = false; return;
    }
  }

  // Rule 7 – warning zone
  bool distWarning = (local.distFront_m < WARNING_DIST_M);
  bool ttcWarning  = (ttc <= WARNING_TTC_S)
                  && (local.speedKmh > MIN_SPEED_KMH);
  if (distWarning || ttcWarning) {
    localWarning = true; return;
  }

  // Rule 8 – all clear
}

// ── OUTPUTS ────────────────────────────────────────────────
void updateOutputs(bool danger, bool warning) {
  bool safe = (!danger && !warning);
  digitalWrite(PIN_LED_GREEN,  safe    ? HIGH : LOW);
  digitalWrite(PIN_LED_YELLOW, warning ? HIGH : LOW);
  digitalWrite(PIN_LED_RED,    danger  ? HIGH : LOW);
  digitalWrite(PIN_BUZZER,     danger  ? HIGH : LOW);
}

// ── DASHBOARD ──────────────────────────────────────────────
void printDashboard(float ttc, bool localDanger, bool localWarning,
                    bool iAmFront) {
  Serial.println(F("\n========================================"));
  Serial.println(F("    V2V DASHBOARD  —  VEHICLE B"));
  Serial.print(  F("    ROLE: "));
  Serial.println(iAmFront ? F("FRONT VEHICLE") : F("REAR VEHICLE"));
  Serial.println(F("========================================"));
  Serial.print(F("LOCAL Speed      : ")); Serial.print(local.speedKmh,1);    Serial.println(F(" km/h"));
  Serial.print(F("LOCAL Distance   : ")); Serial.print(local.distFront_m,2); Serial.println(F(" m"));
  Serial.print(F("LOCAL Brake      : ")); Serial.println(local.brakeActive      ? F("ACTIVE") : F("off"));
  Serial.print(F("LOCAL Obstacle   : ")); Serial.println(local.obstacleDetected ? F("YES <--") : F("clear"));
  Serial.print(F("LOCAL TTC        : ")); Serial.print(ttc,2); Serial.println(F(" s"));
  Serial.println(F("----------------------------------------"));
  if (peerFresh) {
    Serial.print(F("PEER  Speed      : ")); Serial.print(peer.speedKmh,1);    Serial.println(F(" km/h"));
    Serial.print(F("PEER  Distance   : ")); Serial.print(peer.distFront_m,2); Serial.println(F(" m"));
    Serial.print(F("PEER  Brake      : ")); Serial.println(peer.brakeActive      ? F("ACTIVE") : F("off"));
    Serial.print(F("PEER  Obstacle   : ")); Serial.println(peer.obstacleDetected ? F("YES <--") : F("clear"));
    Serial.print(F("PEER  TTC        : ")); Serial.print(peer.ttc_s,2); Serial.println(F(" s"));
  } else {
    Serial.println(F("PEER             : waiting for signal..."));
  }
  Serial.println(F("----------------------------------------"));
  if      (localDanger)  Serial.println(F("STATUS  : *** DANGER  — RED    ***"));
  else if (localWarning) Serial.println(F("STATUS  : !   WARNING — YELLOW  !"));
  else                   Serial.println(F("STATUS  :     SAFE    — GREEN"));
  Serial.print(F("STEER   : "));
  if      (local.steerAdvisory == 1) Serial.println(F("LEFT <<"));
  else if (local.steerAdvisory == 2) Serial.println(F("RIGHT >>"));
  else                               Serial.println(F("STRAIGHT"));
  Serial.println(F("========================================"));
}

void printJsonDashboard(float ttc, bool localDanger,
                    bool localWarning, bool iAmFront) {

  StaticJsonDocument<512> doc;

  doc["vehicle"] = "B";
  doc["role"] = iAmFront ? "FRONT VEHICLE" : "REAR VEHICLE";

  JsonObject localData = doc.createNestedObject("localjson");
  localData["speed_kmh"] = local.speedKmh;
  localData["distance_m"] = local.distFront_m;
  localData["brake_active"] = local.brakeActive;
  localData["obstacle_detected"] = local.obstacleDetected;
  localData["ttc_s"] = ttc;
  localData["steer_advisory"] = local.steerAdvisory;

  if (peerFresh) {
    JsonObject peerData = doc.createNestedObject("peerjson");
    peerData["speed_kmh"] = peer.speedKmh;
    peerData["distance_m"] = peer.distFront_m;
    peerData["brake_active"] = peer.brakeActive;
    peerData["obstacle_detected"] = peer.obstacleDetected;
    peerData["ttc_s"] = peer.ttc_s;
  } else {
    // doc["peer"] = nullptr;
    JsonObject peerData = doc.createNestedObject("peerjson");
    peerData["speed_kmh"] = 0.0;
    peerData["distance_m"] = 0.0;
    peerData["brake_active"] = false;
    peerData["obstacle_detected"] = false;
    peerData["ttc_s"] = 0.0;
  }

  if (localDanger) {
    doc["status"] = "DANGER";
  } else if (localWarning) {
    doc["status"] = "WARNING";
  } else {
    doc["status"] = "SAFE";
  }

  // Arduino Uno UART: Serial (USB)
  serializeJson(doc, Serial);
  Serial.println();
}

// ── ESP-NOW CALLBACKS ──────────────────────────────────────
void onSentImpl(const uint8_t *, esp_now_send_status_t) {}

void onReceive(const esp_now_recv_info_t *,
               const uint8_t *data, int len) {
  if (len == sizeof(VehicleData)) {
    memcpy(&peer, data, sizeof(VehicleData));
    peerFresh  = true;
    lastPeerRx = millis();
  }
}

// ── SETUP ──────────────────────────────────────────────────
void setup() {
  Serial.begin(115200);
  delay(300);

  pinMode(PIN_TRIG,       OUTPUT);
  pinMode(PIN_ECHO,       INPUT);
  pinMode(PIN_LED_GREEN,  OUTPUT);
  pinMode(PIN_LED_YELLOW, OUTPUT);
  pinMode(PIN_LED_RED,    OUTPUT);
  pinMode(PIN_BUZZER,     OUTPUT);

  digitalWrite(PIN_LED_GREEN,  LOW);
  digitalWrite(PIN_LED_YELLOW, LOW);
  digitalWrite(PIN_LED_RED,    LOW);
  digitalWrite(PIN_BUZZER,     LOW);

  analogReadResolution(12);
  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  delay(1000);

  Serial.println(F("\n=== Vehicle B booting (v5) ==="));
  Serial.print(F("Vehicle B MAC: "));
  Serial.println(WiFi.macAddress());
  Serial.println(F(">>> Copy this MAC into vehicle_A_v5.ino PEER_MAC[] <<<\n"));

  if (esp_now_init() != ESP_OK) {
    Serial.println(F("[FATAL] ESP-NOW init failed. Halting."));
    while (true) delay(1000);
  }

  esp_now_register_send_cb((esp_now_send_cb_t)onSentImpl);
  esp_now_register_recv_cb(onReceive);

  esp_now_peer_info_t p = {};
  memcpy(p.peer_addr, PEER_MAC, 6);
  p.channel = 0;
  p.encrypt = false;
  if (esp_now_add_peer(&p) != ESP_OK)
    Serial.println(F("[WARN] Peer add failed — check PEER_MAC array."));
  else
    Serial.println(F("[OK] Peer registered. Starting loop."));

  local.id = 2;
}

// ── LOOP ───────────────────────────────────────────────────
void loop() {
  uint32_t now = millis();

  if (peerFresh && (now - lastPeerRx > PEER_TIMEOUT_MS)) {
    peerFresh = false;
  }

  float distM    = readDistanceCm() / 100.0f;
  float speedKmh = readSpeedKmh();
  bool  brake    = readBrake();
  bool  obstacle = (distM < DANGER_DIST_M);

  local.speedKmh         = speedKmh;
  local.distFront_m      = distM;
  local.brakeActive      = brake;
  local.obstacleDetected = obstacle;
  local.ts               = now;

  bool iAmFront = ROLE_IS_FRONT;

  float ttc;
  if (peerFresh && !iAmFront && peer.speedKmh > MIN_SPEED_KMH) {
    ttc = computeTTC(speedKmh, peer.speedKmh, distM);
  } else if (peerFresh && iAmFront) {
    ttc = peer.ttc_s;
  } else if (speedKmh > MIN_SPEED_KMH && distM < WARNING_DIST_M) {
    ttc = distM / (speedKmh / 3.6f);
  } else {
    ttc = 9999.0f;
  }

  local.ttc_s         = ttc;
  local.steerAdvisory = calcSteer(ttc);

  bool localDanger, localWarning, peerShouldAlert;
  computeDanger(ttc, iAmFront, localDanger, localWarning, peerShouldAlert);

  updateOutputs(localDanger, localWarning);

  if (now - lastTx >= TX_INTERVAL_MS) {
    esp_now_send(PEER_MAC, (uint8_t *)&local, sizeof(VehicleData));
    lastTx = now;
  }

  if (now - lastPrint >= PRINT_INTERVAL_MS) {
    // printDashboard(ttc, localDanger, localWarning, iAmFront);
    printJsonDashboard(ttc, localDanger, localWarning, iAmFront);
    lastPrint = now;
  }

  delay(5);
}

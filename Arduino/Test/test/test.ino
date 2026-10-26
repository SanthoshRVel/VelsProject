
#include <ArduinoJson.h>

// Vehicle data structure
struct VehicleData {
  float speedKmh;
  float distFront_m;
  bool brakeActive;
  bool obstacleDetected;
  float ttc_s;
  int steerAdvisory;
};

VehicleData local;
VehicleData peer;

bool peerFresh = true;

// Generate random vehicle data
void generateRandomData() {
  local.speedKmh = random(0, 121);
  local.distFront_m = random(100, 3001) / 100.0;
  local.brakeActive = random(0, 2);
  local.obstacleDetected = random(0, 2);
  local.ttc_s = random(0, 501) / 100.0;
  local.steerAdvisory = random(0, 3);

  peer.speedKmh = random(0, 121);
  peer.distFront_m = random(100, 3001) / 100.0;
  peer.brakeActive = random(0, 2);
  peer.obstacleDetected = random(0, 2);
  peer.ttc_s = random(0, 501) / 100.0;
  peer.steerAdvisory = random(0, 3);
}

// Your JSON dashboard function
void printDashboard(float ttc, bool localDanger,
                    bool localWarning, bool iAmFront) {

  StaticJsonDocument<512> doc;

  doc["vehicle"] = "A";
  doc["role"] = iAmFront ? "FRONT VEHICLE" : "REAR VEHICLE";

  JsonObject localData = doc.createNestedObject("local");
  localData["speed_kmh"] = local.speedKmh;
  localData["distance_m"] = local.distFront_m;
  localData["brake_active"] = local.brakeActive;
  localData["obstacle_detected"] = local.obstacleDetected;
  localData["ttc_s"] = ttc;
  localData["steer_advisory"] = local.steerAdvisory;

  if (peerFresh) {
    JsonObject peerData = doc.createNestedObject("peer");
    peerData["speed_kmh"] = peer.speedKmh;
    peerData["distance_m"] = peer.distFront_m;
    peerData["brake_active"] = peer.brakeActive;
    peerData["obstacle_detected"] = peer.obstacleDetected;
    peerData["ttc_s"] = peer.ttc_s;
  } else {
    doc["peer"] = nullptr;
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

void setup() {
  Serial.begin(115200);
  // randomSeed(analogRead(A0));
}

void loop() {
  static unsigned long lastUpdate = 0;
  unsigned long now = millis();

  if (now - lastUpdate >= 500) {
    lastUpdate = now;

    generateRandomData();

    // Determine status from generated data
    bool localDanger =
        local.obstacleDetected ||
        (local.ttc_s > 0 && local.ttc_s < 2.0);

    bool localWarning =
        !localDanger &&
        (local.brakeActive || local.ttc_s < 4.0);

    printDashboard(
      local.ttc_s,
      localDanger,
      localWarning,
      true  // This vehicle is the front vehicle
    );
  }
}

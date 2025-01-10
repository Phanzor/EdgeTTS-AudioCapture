document.addEventListener("DOMContentLoaded", () => {
    const textToSpeak = document.getElementById("textToSpeak");
    const voiceSelect = document.getElementById("voiceSelect");
    const voiceSearch = document.getElementById("voiceSearch");
    const voiceFilters = document.getElementById("voiceFilters");
    const playTTSButton = document.getElementById("playTTS");
    const startRecordingButton = document.getElementById("startRecording");
    const stopRecordingButton = document.getElementById("stopRecording");
    const recordingsList = document.getElementById("recordingsList");
    let isRecording = false;

    // Fetch and populate voices
    function populateVoices() {
        const voices = speechSynthesis.getVoices();
        const searchQuery = voiceSearch.value.toLowerCase();
        const selectedFilters = Array.from(voiceFilters.querySelectorAll("input:checked")).map(input => input.value);

        const filteredVoices = voices.filter(voice => {
            if (searchQuery && !voice.name.toLowerCase().includes(searchQuery)) return false;

            // Filter logic
            if (selectedFilters.includes("online") && !voice.name.toLowerCase().includes("online")) return false;
            if (selectedFilters.includes("language") && !voice.lang.toLowerCase().includes("en")) return false;
            if (selectedFilters.includes("multilingual") && !voice.name.toLowerCase().includes("multi")) return false;

            return true;
        });

        voiceSelect.innerHTML = filteredVoices
            .map(voice => `<option value="${voice.name}">${voice.name} (${voice.lang})</option>`)
            .join("");

        // Set default to "Microsoft Ava Online" if available
        const defaultVoice = filteredVoices.find(voice => voice.name.includes("Microsoft Ava Online"));
        if (defaultVoice) {
            voiceSelect.value = defaultVoice.name;
        }
    }

    // Ensure voices are populated once available
    if (speechSynthesis.onvoiceschanged !== undefined) {
        speechSynthesis.onvoiceschanged = populateVoices;
    }
    populateVoices();

    // Search and filter logic
    voiceSearch.addEventListener("input", populateVoices);
    voiceFilters.addEventListener("change", populateVoices);

    // Play TTS
    playTTSButton.addEventListener("click", async () => {
        const text = textToSpeak.value.trim();
        const selectedVoiceName = voiceSelect.value;

        if (!text) {
            alert("Please enter text to speak.");
            return;
        }

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.voice = speechSynthesis.getVoices().find(voice => voice.name === selectedVoiceName);

        if (!isRecording) {
            await startRecording(selectedVoiceName);
        }

        utterance.onend = async () => {
            if (isRecording) {
                await stopRecording();
            }
        };

        speechSynthesis.speak(utterance);
    });

    async function startRecording(selectedVoice) {
        if (isRecording) return;

        try {
            const response = await fetch(`http://localhost:5000/record?voice=${encodeURIComponent(selectedVoice)}`, {
                method: "POST",
            });
            if (!response.ok) throw new Error(await response.text());

            console.log(await response.text());
            isRecording = true;
            startRecordingButton.disabled = true;
            stopRecordingButton.disabled = false;
        } catch (error) {
            console.error("Error starting recording:", error.message);
            alert("Failed to start recording. Check console for details.");
        }
    }

    async function stopRecording() {
        try {
            const response = await fetch("http://localhost:5000/stop", { method: "POST" });
            if (!response.ok) throw new Error("Failed to stop recording.");
            isRecording = false;
            startRecordingButton.disabled = false;
            stopRecordingButton.disabled = true;
            fetchRecordings();
        } catch (error) {
            console.error("Error stopping recording:", error.message);
        }
    }

    async function fetchRecordings() {
        const response = await fetch("http://localhost:5000/recordings");
        if (response.ok) {
            const recordings = await response.json();
            recordingsList.innerHTML = recordings
                .reverse()
                .map(recording => {
                    const [voice, timestamp] = recording.replace(".wav", "").split("_");
                    return `
                        <li>
                            <span><strong>Voice:</strong> ${voice || "Unknown"}</span> - 
                            <span><strong>Timestamp:</strong> ${timestamp || "N/A"}</span>
                            <audio controls src="http://localhost:5000/recordings/${recording}"></audio>
                            <button onclick="deleteRecording('${recording}')">Delete</button>
                        </li>
                    `;
                })
                .join("");
        }
    }

    window.deleteRecording = async function (filename) {
        const response = await fetch(`http://localhost:5000/recordings/${filename}`, { method: "DELETE" });
        if (response.ok) {
            fetchRecordings();
        } else {
            alert("Failed to delete recording.");
        }
    };

    fetchRecordings();
});
